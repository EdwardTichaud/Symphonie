using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[RequireComponent(typeof(PlayableDirector))]
public class SetTimelineBidings : MonoBehaviour
{
    [Serializable]
    private class SignalBinding
    {
        public SignalAsset signal;
        public MonoBehaviour target;
        public UnityEngine.Object targetScript;
        public string targetTypeName;
        public string methodName;
        public List<SignalArgument> arguments = new();
    }

    [Serializable]
    private class SignalArgument
    {
        public enum ArgumentType
        {
            Bool,
            Int,
            Float,
            String,
            Object
        }

        public ArgumentType type;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
        public UnityEngine.Object objectValue;
    }

    private struct SignalBindingKey : IEquatable<SignalBindingKey>
    {
        private readonly int receiverId;
        private readonly int signalId;
        private readonly int targetId;
        private readonly int methodHash;
        private readonly int argumentsHash;

        public SignalBindingKey(SignalReceiver receiver, SignalAsset signal, UnityEngine.Object target, string methodName, int argumentsHash)
        {
            receiverId = receiver != null ? receiver.GetInstanceID() : 0;
            signalId = signal != null ? signal.GetInstanceID() : 0;
            targetId = target != null ? target.GetInstanceID() : 0;
            methodHash = string.IsNullOrEmpty(methodName) ? 0 : StringComparer.Ordinal.GetHashCode(methodName);
            this.argumentsHash = argumentsHash;
        }

        public bool Equals(SignalBindingKey other)
        {
            return receiverId == other.receiverId
                && signalId == other.signalId
                && targetId == other.targetId
                && methodHash == other.methodHash
                && argumentsHash == other.argumentsHash;
        }

        public override bool Equals(object obj)
        {
            return obj is SignalBindingKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + receiverId;
                hash = hash * 31 + signalId;
                hash = hash * 31 + targetId;
                hash = hash * 31 + methodHash;
                hash = hash * 31 + argumentsHash;
                return hash;
            }
        }
    }

    [Header("Bindings")]
    [SerializeField] private PlayableDirector playableDirector;
    [SerializeField] private List<string> bindingTags = new();

    [Header("Signal Receiver")]
    [SerializeField] private SignalReceiver signalReceiver;
    [SerializeField] private List<SignalBinding> signalBindings = new();

    private readonly HashSet<SignalBindingKey> registeredSignalBindings = new();

    private void Awake()
    {
        ApplyBindings();
    }

    private void OnEnable()
    {
        ApplyBindings();
    }

    private void OnValidate()
    {
        if (signalBindings == null || signalBindings.Count == 0)
            return;

        foreach (var binding in signalBindings)
        {
            if (binding == null)
                continue;

            if (binding.targetScript != null)
            {
                Type scriptType = GetScriptClass(binding.targetScript);
                if (scriptType != null)
                {
                    binding.targetTypeName = scriptType.FullName;
                    continue;
                }
            }

            if (binding.target != null && string.IsNullOrWhiteSpace(binding.targetTypeName))
                binding.targetTypeName = binding.target.GetType().FullName;
        }
    }

    public void ApplyBindings()
    {
        ApplyTimelineBindings();
        ApplySignalBindings();
    }

    private void ApplyTimelineBindings()
    {
        if (playableDirector == null)
            playableDirector = GetComponent<PlayableDirector>();

        if (playableDirector == null)
        {
            Debug.LogWarning("[SetTimelineBidings] Missing PlayableDirector.");
            return;
        }

        if (bindingTags == null || bindingTags.Count == 0)
            return;

        var asset = playableDirector.playableAsset;
        if (asset == null)
        {
            Debug.LogWarning("[SetTimelineBidings] Missing PlayableAsset.");
            return;
        }

        int index = 0;
        foreach (var output in asset.outputs)
        {
            if (index >= bindingTags.Count)
            {
                Debug.LogWarning($"[SetTimelineBidings] Missing tag for binding index {index} ({output.streamName}).");
                index++;
                continue;
            }

            string tag = bindingTags[index];
            index++;

            if (string.IsNullOrWhiteSpace(tag))
            {
                Debug.LogWarning($"[SetTimelineBidings] Empty tag for binding index {index - 1} ({output.streamName}).");
                continue;
            }

            GameObject taggedObject = FindGameObjectByTag(tag);
            if (taggedObject == null)
            {
                Debug.LogWarning($"[SetTimelineBidings] No GameObject found with tag '{tag}'.");
                continue;
            }

            UnityEngine.Object bindingTarget = ResolveBindingTarget(output, taggedObject);
            if (bindingTarget == null)
            {
                Debug.LogWarning($"[SetTimelineBidings] No compatible binding target for '{output.streamName}' on '{taggedObject.name}'.");
                continue;
            }

            playableDirector.SetGenericBinding(output.sourceObject, bindingTarget);
        }

        if (bindingTags.Count > index)
        {
            Debug.LogWarning($"[SetTimelineBidings] {bindingTags.Count - index} tag(s) were not used.");
        }
    }

    private void ApplySignalBindings()
    {
        if (signalBindings == null || signalBindings.Count == 0)
            return;

        if (signalReceiver == null)
            signalReceiver = GetComponent<SignalReceiver>();

        if (signalReceiver == null)
            signalReceiver = gameObject.AddComponent<SignalReceiver>();

        foreach (var binding in signalBindings)
        {
            if (binding == null)
                continue;

            if (binding.signal == null)
            {
                Debug.LogWarning("[SetTimelineBidings] Missing SignalAsset in signal bindings.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(binding.methodName))
            {
                Debug.LogWarning($"[SetTimelineBidings] Missing method name for signal '{binding.signal.name}'.");
                continue;
            }

            MonoBehaviour target = ResolveSignalTarget(binding);
            if (target == null)
                continue;

            int argumentsHash = ComputeArgumentsHash(binding.arguments);
            var key = new SignalBindingKey(signalReceiver, binding.signal, target, binding.methodName, argumentsHash);
            if (registeredSignalBindings.Contains(key))
                continue;

            if (!TryResolveSignalMethod(binding, target, out MethodInfo method, out object[] arguments))
            {
                continue;
            }

            UnityAction action = () => InvokeSignalMethod(target, method, arguments);

            UnityEvent reaction = signalReceiver.GetReaction(binding.signal);
            if (reaction == null)
            {
                reaction = new UnityEvent();
                signalReceiver.AddReaction(binding.signal, reaction);
            }

            reaction.AddListener(action);
            registeredSignalBindings.Add(key);
        }
    }

    private MonoBehaviour ResolveSignalTarget(SignalBinding binding)
    {
        if (binding == null)
            return null;

        if (binding.target != null)
            return binding.target;

        Type targetType = ResolveTargetType(binding);
        if (targetType == null)
        {
            string signalName = binding.signal != null ? binding.signal.name : "Unknown";
            Debug.LogWarning($"[SetTimelineBidings] Missing target type for signal '{signalName}'.");
            return null;
        }

        if (!typeof(MonoBehaviour).IsAssignableFrom(targetType))
        {
            Debug.LogWarning($"[SetTimelineBidings] Target type '{targetType.Name}' is not a MonoBehaviour.");
            return null;
        }

        var component = GetComponent(targetType) as MonoBehaviour;
        if (component != null)
            return component;

        component = GetComponentInChildren(targetType, includeInactive: true) as MonoBehaviour;
        if (component != null)
            return component;

        int foundCount;
        MonoBehaviour found = FindFirstInScene(targetType, out foundCount);
        if (found == null)
        {
            Debug.LogWarning($"[SetTimelineBidings] Target type '{targetType.Name}' not found in scene.");
            return null;
        }

        if (foundCount > 1)
        {
            Debug.LogWarning($"[SetTimelineBidings] Multiple targets found for type '{targetType.Name}'. Using '{found.name}'.");
        }

        return found;
    }

    private static Type ResolveTargetType(SignalBinding binding)
    {
        if (binding == null)
            return null;

        string typeName = binding.targetTypeName;
        if (string.IsNullOrWhiteSpace(typeName) && binding.targetScript != null)
        {
            Type scriptType = GetScriptClass(binding.targetScript);
            if (scriptType != null)
                typeName = scriptType.FullName;
        }

        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        bool multipleMatches;
        Type resolved = ResolveTargetType(typeName, out multipleMatches);
        if (resolved == null)
            return null;

        if (multipleMatches)
        {
            Debug.LogWarning($"[SetTimelineBidings] Multiple types named '{typeName}' found. Using '{resolved.FullName}'.");
        }

        return resolved;
    }

    private static Type ResolveTargetType(string typeName, out bool multipleMatches)
    {
        multipleMatches = false;
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        Type type = Type.GetType(typeName, throwOnError: false);
        if (type != null)
            return type;

        Type found = null;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            if (types == null)
                continue;

            foreach (var candidate in types)
            {
                if (candidate == null)
                    continue;

                if (!string.Equals(candidate.FullName, typeName, StringComparison.Ordinal)
                    && !string.Equals(candidate.Name, typeName, StringComparison.Ordinal))
                    continue;

                if (found == null)
                {
                    found = candidate;
                }
                else if (found != candidate)
                {
                    multipleMatches = true;
                }
            }
        }

        return found;
    }

    private static Type GetScriptClass(UnityEngine.Object scriptAsset)
    {
        if (scriptAsset == null)
            return null;

        MethodInfo method = scriptAsset.GetType().GetMethod("GetClass", BindingFlags.Instance | BindingFlags.Public);
        if (method == null || method.ReturnType != typeof(Type))
            return null;

        try
        {
            return method.Invoke(scriptAsset, null) as Type;
        }
        catch (TargetInvocationException exception)
        {
            Debug.LogException(exception.InnerException ?? exception);
            return null;
        }
    }

    private static MonoBehaviour FindFirstInScene(Type type, out int foundCount)
    {
        foundCount = 0;
        if (type == null)
            return null;

        MonoBehaviour first = null;
        var objects = Resources.FindObjectsOfTypeAll(type);
        if (objects == null)
            return null;

        foreach (var obj in objects)
        {
            var component = obj as MonoBehaviour;
            if (component == null)
                continue;

            var scene = component.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            if (first == null)
                first = component;

            foundCount++;
        }

        return first;
    }

    private static bool TryResolveSignalMethod(SignalBinding binding, MonoBehaviour target, out MethodInfo method, out object[] arguments)
    {
        method = null;
        arguments = null;

        if (binding == null || target == null)
            return false;

        int argumentCount = binding.arguments != null ? binding.arguments.Count : 0;
        var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var candidate in methods)
        {
            if (!string.Equals(candidate.Name, binding.methodName, StringComparison.Ordinal))
                continue;

            if (candidate.ReturnType != typeof(void))
                continue;

            if (!TryBuildArguments(candidate, binding.arguments, out object[] builtArguments))
                continue;

            method = candidate;
            arguments = builtArguments;
            return true;
        }

        Debug.LogWarning(
            $"[SetTimelineBidings] Method '{binding.methodName}' with {argumentCount} argument(s) not found on '{target.GetType().Name}'.");
        return false;
    }

    private static bool TryBuildArguments(MethodInfo method, List<SignalArgument> arguments, out object[] builtArguments)
    {
        builtArguments = null;
        var parameters = method.GetParameters();
        int argumentCount = arguments != null ? arguments.Count : 0;

        if (parameters.Length != argumentCount)
            return false;

        if (argumentCount == 0)
        {
            builtArguments = Array.Empty<object>();
            return true;
        }

        builtArguments = new object[argumentCount];
        for (int i = 0; i < argumentCount; i++)
        {
            var parameter = parameters[i];
            if (parameter.ParameterType.IsByRef || parameter.IsOut)
                return false;

            if (!TryConvertArgument(arguments[i], parameter.ParameterType, out object value))
                return false;

            builtArguments[i] = value;
        }

        return true;
    }

    private static bool TryConvertArgument(SignalArgument argument, Type parameterType, out object value)
    {
        value = null;

        if (parameterType == null)
            return false;

        if (argument == null)
            return false;

        if (typeof(UnityEngine.Object).IsAssignableFrom(parameterType))
        {
            if (argument.type != SignalArgument.ArgumentType.Object)
                return false;

            if (argument.objectValue == null)
            {
                value = null;
                return true;
            }

            if (parameterType.IsInstanceOfType(argument.objectValue))
            {
                value = argument.objectValue;
                return true;
            }

            return false;
        }

        if (parameterType.IsEnum)
        {
            if (argument.type == SignalArgument.ArgumentType.Int)
            {
                value = Enum.ToObject(parameterType, argument.intValue);
                return true;
            }

            if (argument.type == SignalArgument.ArgumentType.String && !string.IsNullOrWhiteSpace(argument.stringValue))
            {
                try
                {
                    value = Enum.Parse(parameterType, argument.stringValue, ignoreCase: true);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            return false;
        }

        switch (Type.GetTypeCode(parameterType))
        {
            case TypeCode.Boolean:
                if (argument.type == SignalArgument.ArgumentType.Bool)
                {
                    value = argument.boolValue;
                    return true;
                }
                return false;
            case TypeCode.Int32:
                if (argument.type == SignalArgument.ArgumentType.Int)
                {
                    value = argument.intValue;
                    return true;
                }
                return false;
            case TypeCode.Single:
                if (argument.type == SignalArgument.ArgumentType.Float)
                {
                    value = argument.floatValue;
                    return true;
                }
                return false;
            case TypeCode.String:
                if (argument.type == SignalArgument.ArgumentType.String)
                {
                    value = argument.stringValue;
                    return true;
                }
                return false;
            case TypeCode.Double:
                if (argument.type == SignalArgument.ArgumentType.Float)
                {
                    value = (double)argument.floatValue;
                    return true;
                }
                if (argument.type == SignalArgument.ArgumentType.Int)
                {
                    value = (double)argument.intValue;
                    return true;
                }
                return false;
            case TypeCode.Int64:
                if (argument.type == SignalArgument.ArgumentType.Int)
                {
                    value = (long)argument.intValue;
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static int ComputeArgumentsHash(List<SignalArgument> arguments)
    {
        if (arguments == null || arguments.Count == 0)
            return 0;

        unchecked
        {
            int hash = 17;
            foreach (var argument in arguments)
            {
                if (argument == null)
                {
                    hash = hash * 31;
                    continue;
                }

                hash = hash * 31 + (int)argument.type;
                switch (argument.type)
                {
                    case SignalArgument.ArgumentType.Bool:
                        hash = hash * 31 + (argument.boolValue ? 1 : 0);
                        break;
                    case SignalArgument.ArgumentType.Int:
                        hash = hash * 31 + argument.intValue;
                        break;
                    case SignalArgument.ArgumentType.Float:
                        hash = hash * 31 + argument.floatValue.GetHashCode();
                        break;
                    case SignalArgument.ArgumentType.String:
                        hash = hash * 31 + (string.IsNullOrEmpty(argument.stringValue) ? 0 : StringComparer.Ordinal.GetHashCode(argument.stringValue));
                        break;
                    case SignalArgument.ArgumentType.Object:
                        hash = hash * 31 + (argument.objectValue != null ? argument.objectValue.GetInstanceID() : 0);
                        break;
                }
            }

            return hash;
        }
    }

    private static void InvokeSignalMethod(MonoBehaviour target, MethodInfo method, object[] arguments)
    {
        if (target == null || method == null)
            return;

        try
        {
            method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception)
        {
            Debug.LogException(exception.InnerException ?? exception);
        }
    }

    private static GameObject FindGameObjectByTag(string tag)
    {
        if (SceneBindings.TryGetByTag(tag, out GameObject bound) && bound != null)
            return bound;

        Debug.LogWarning($"[SetTimelineBidings] Tag '{tag}' non bindee dans SceneBindings.");
        return null;
    }

    private static UnityEngine.Object ResolveBindingTarget(PlayableBinding output, GameObject taggedObject)
    {
        var targetType = output.outputTargetType;
        if (targetType == null || targetType == typeof(GameObject))
            return taggedObject;

        if (typeof(Component).IsAssignableFrom(targetType))
        {
            var component = taggedObject.GetComponent(targetType);
            if (component != null)
                return component;

            component = taggedObject.GetComponentInChildren(targetType, includeInactive: true);
            if (component != null)
                return component;

            return null;
        }

        return targetType.IsInstanceOfType(taggedObject) ? taggedObject : null;
    }
}
