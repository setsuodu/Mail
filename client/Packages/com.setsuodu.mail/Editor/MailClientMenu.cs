using UnityEditor;
using UnityEngine;
using Setsuodu.Mail;

namespace Setsuodu.Mail.Editor
{
    public static class MailClientMenu
    {
        [MenuItem("Tools/Mail/Create MailClient GameObject")]
        public static void CreateMailClient()
        {
            var existing = Object.FindObjectOfType<MailClient>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[Mail] MailClient already exists in scene.");
                return;
            }

            var go = new GameObject("MailClient");
            go.AddComponent<MailClient>();
            Undo.RegisterCreatedObjectUndo(go, "Create MailClient");
            Selection.activeGameObject = go;
            Debug.Log("[Mail] MailClient created. Configure Server Base Url & set JWT after login.");
        }
    }
}
