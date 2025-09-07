using UnityEngine;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Symphonie/Tools/Rename Children By First")]
public class RenameChildrenByFirst : MonoBehaviour
{
    [Tooltip("Renommer aussi le premier enfant (sinon on le laisse tel quel).")]
    public bool includeFirst = false;

    [Tooltip("Préserver le nombre de chiffres (ex: 001 → 002).")]
    public bool preserveNumberWidth = true;

#if UNITY_EDITOR
    [ContextMenu("Renommer enfants selon le premier")]
    public void RenameNow_ContextMenu() => RenameNow();
#endif

    public void RenameNow()
    {
#if UNITY_EDITOR
        Rename(transform, includeFirst, preserveNumberWidth);
#else
        Debug.LogWarning("Cet outil s'utilise dans l'éditeur Unity.");
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Renomme les enfants directs d'un parent selon le premier enfant.
    /// </summary>
    public static void Rename(Transform parent, bool includeFirst, bool preserveWidth)
    {
        if (parent == null) return;
        int childCount = parent.childCount;
        if (childCount == 0) return;

        Transform first = parent.GetChild(0);
        var parsed = ParseName(first.name);

        string prefix = parsed.prefix;
        int number = parsed.number;
        int width = parsed.width;

        // Si aucun suffixe numérique n'est trouvé, on part de 1 et on force un séparateur "_"
        if (number < 0)
        {
            number = 1;
            width = preserveWidth ? 1 : 0;
            // Ajoute "_" si pas déjà présent à la fin du préfixe
            if (!prefix.EndsWith("_"))
                prefix = prefix + "_";
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName($"Rename Children By First: {parent.name}");
        int group = Undo.GetCurrentGroup();

        int index = number;

        for (int i = 0; i < childCount; i++)
        {
            var child = parent.GetChild(i);

            // Si on ne renomme pas le premier, on passe au suivant mais on avance la numérotation
            if (i == 0 && !includeFirst)
            {
                index++;
                continue;
            }

            string numStr = preserveWidth && width > 0
                ? index.ToString(new string('0', width))
                : index.ToString();

            string newName = prefix + numStr;

            if (child.name != newName)
            {
                Undo.RecordObject(child.gameObject, "Rename Child");
                child.name = newName;
                EditorUtility.SetDirty(child.gameObject);
            }

            index++;
        }

        Undo.CollapseUndoOperations(group);
    }

    /// <summary>
    /// Extrait (prefixe, nombre, largeur) depuis le nom. 
    /// Renvoie number = -1 si pas de suffixe numérique trouvé.
    /// </summary>
    private static (string prefix, int number, int width) ParseName(string name)
    {
        // Cas général : toute fin de chaîne se terminant par des chiffres
        var m = Regex.Match(name, @"^(.*?)(\d+)$");
        if (m.Success)
        {
            string p = m.Groups[1].Value;
            string n = m.Groups[2].Value;
            return (p, int.Parse(n), n.Length);
        }

        // Variante tolérante: "prefix_ 12" / "prefix-12" / "prefix 12"
        m = Regex.Match(name, @"^(.*?)(?:_|-|\s)(\d+)$");
        if (m.Success)
        {
            string p = m.Groups[1].Value + "_";
            string n = m.Groups[2].Value;
            return (p, int.Parse(n), n.Length);
        }

        // Aucun nombre détecté
        return (name, -1, 0);
    }
#endif
}
