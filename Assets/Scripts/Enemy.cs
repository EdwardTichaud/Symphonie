using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.UI;
using System.Collections;

public class Enemy : MonoBehaviour
{
    public CharacterData enemyData;
    public bool wasPartOfLastBattle;
    public bool isDead;

    private bool dissolveFadeOn;
    private List<Material> dissolveMaterials = new List<Material>();

    public Material[] allMats;

    void Awake()
    {
        InitializeEnemy();
    }

    private void InitializeEnemy()
    {
        GameObject enemyBody = Instantiate(enemyData.characterWorldModel, transform.position, Quaternion.identity);
        enemyBody.name = "Enemy_Body" + enemyData.name;
        enemyBody.transform.SetParent(transform);

        // ✅ Appliquer le layer "Enemy" à enemyBody et tous ses enfants
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        SetLayerRecursively(enemyBody, enemyLayer);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    void Update()
    {
        if (wasPartOfLastBattle)
        {
            // Lance la progression du dissolve tant que l'effet est activé
            if (!dissolveFadeOn)
            {
                PlayDissolve();

                if (!isDead)
                {
                    StartCoroutine(Die());
                }
            }
        }
    }

    public void DissolveFadeOn()
    {
        SetupMaterials();
        dissolveFadeOn = true;
    }

    public void DissolveFadeOff()
    {
        SetupMaterials();
        dissolveFadeOn = false;
    }

    private void SetupMaterials()
    {
        dissolveMaterials.Clear();
        allMats = GetAllMaterials(gameObject);
        dissolveMaterials.AddRange(allMats);
    }

    public static Material[] GetAllMaterials(GameObject root, bool includeInactive = true)
    {
        var mats = new List<Material>();
        var components = root.GetComponentsInChildren<Component>(includeInactive);

        foreach (var comp in components)
        {
            if (comp is Renderer rend)
            {
                mats.AddRange(rend.materials);  // récupère toutes les instances de matériaux
                continue;
            }

            if (comp is Graphic ui)
            {
                mats.Add(ui.material);
                continue;
            }

            // Réflexion pour tout autre champ/propriété Material
            var type = comp.GetType();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (prop.PropertyType == typeof(Material) && prop.CanRead && prop.GetIndexParameters().Length == 0)
                {
                    var mat = prop.GetValue(comp, null) as Material;
                    if (mat != null) mats.Add(mat);
                }
                else if (prop.PropertyType == typeof(Material[]) && prop.CanRead)
                {
                    var arr = prop.GetValue(comp, null) as Material[];
                    if (arr != null) mats.AddRange(arr);
                }
            }

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Material))
                {
                    var mat = field.GetValue(comp) as Material;
                    if (mat != null) mats.Add(mat);
                }
                else if (field.FieldType == typeof(Material[]))
                {
                    var arr = field.GetValue(comp) as Material[];
                    if (arr != null) mats.AddRange(arr);
                }
            }
        }

        return mats.Distinct().ToArray();
    }

    private void PlayDissolve()
    {
        bool allCompleted = true;
        float delta = Time.unscaledDeltaTime * 2f;

        foreach (var mat in dissolveMaterials)
        {
            if (mat == null) continue;

            float current = mat.GetFloat("_Fade");
            float target = dissolveFadeOn ? 1f : 0f;
            float next = Mathf.MoveTowards(current, target, delta);
            mat.SetFloat("_Fade", next);

            if (dissolveFadeOn && next < 1f)
                allCompleted = false;
            if (!dissolveFadeOn && next > 0f)
                allCompleted = false;
        }
    }

    IEnumerator Die()
    {
        yield return new WaitForSeconds(2f); // Attendre
        while (NewBattleManager.Instance.currentBattleState != NewBattleManager.BattleState.None) { yield return null; }        
        yield return new WaitForSeconds(2f); // Attendre que l'effet de dissolve soit terminé
        isDead = true;
        Destroy(transform.parent.gameObject);
        // Ajouter ici ta logique de mort (destruction, pooling, animation, etc.)
        Debug.Log($"Enemy {gameObject.name} is now dead.");
        // Par exemple : Destroy(gameObject);
    }
}