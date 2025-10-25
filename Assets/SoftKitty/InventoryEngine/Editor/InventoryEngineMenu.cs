using UnityEditor;
using UnityEngine;
using System.Collections;

namespace SoftKitty.InventoryEngine
{
	public class InventoryEngineMenu : ScriptableWizard
	{

		[MenuItem("Window/Inventory Engine/TextMeshPro Converter")]

		public static void CreateWizard()
		{
			EditorWindow.GetWindow<InventoryEngineTool>(false, "InventoryEngine Tool", true).Show();
		}

	}
}
