using System.Collections.Generic;
using UnityEngine;

public class SquadManager : MonoBehaviour
{
    public static SquadManager Instance { get; private set; }

    [SerializeField] private List<CharacterData> squadCharacters = new();

    public IReadOnlyList<CharacterData> SquadCharacters => squadCharacters;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetSquad(List<CharacterData> characters)
    {
        squadCharacters = new List<CharacterData>(characters);
    }

    public void MoveCharacter(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= squadCharacters.Count ||
            toIndex < 0 || toIndex >= squadCharacters.Count ||
            fromIndex == toIndex)
            return;

        CharacterData cd = squadCharacters[fromIndex];
        squadCharacters.RemoveAt(fromIndex);
        squadCharacters.Insert(toIndex, cd);
    }
}
