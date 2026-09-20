using UnityEditor;
using UnityEngine;

namespace TenteEEEE.HUDClock.Editor
{
    public static class HUDClockMenu
    {
        [MenuItem("GameObject/HUD Clock", false, 20)]
        private static void Create(MenuCommand command)
        {
            GameObject gameObject = new GameObject("HUDClock");
            GameObjectUtility.SetParentAndAlign(gameObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create HUD Clock");
            gameObject.AddComponent<TenteEEEE.HUDClock.HUDClockComponent>();
            Selection.activeGameObject = gameObject;
        }
    }
}
