using System;
using System.Collections;
using System.Text;
using Setsuodu.Mail.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace Setsuodu.Mail.Samples
{
    /// <summary>
    /// End-to-end Mail API sample. Add MailClient + this component to a scene.
    /// Requires local server (docker compose up --build in server/).
    /// </summary>
    public sealed class MailInboxSample : MonoBehaviour
    {
        [Header("Server (must match docker-compose / .env)")]
        [SerializeField] private string serverBaseUrl = "http://localhost:12081";
        [SerializeField] private string projectId = "default";
        [SerializeField] private string adminApiKey = "dev-admin-key";
        [SerializeField] private string jwtSecret = "dev-jwt-secret-shared-with-mp";
        [SerializeField] private string sampleUserId = "sample-user-1";

        [Header("Keys")]
        [SerializeField] private KeyCode seedAdminMailKey = KeyCode.F1;
        [SerializeField] private KeyCode refreshKey = KeyCode.F2;
        [SerializeField] private KeyCode markFirstReadKey = KeyCode.F3;
        [SerializeField] private KeyCode claimFirstKey = KeyCode.F4;
        [SerializeField] private KeyCode deleteFirstKey = KeyCode.F5;
        [SerializeField] private KeyCode runFullFlowKey = KeyCode.F6;

        private bool _busy;

        private void Start()
        {
            EnsureMailClient();
            ApplyJwt();
            Debug.Log("[MailSample] Ready. F1 seed | F2 refresh | F3 read | F4 claim | F5 delete | F6 full flow");
        }

        private void Update()
        {
            if (!Application.isPlaying || _busy) return;
            if (Input.GetKeyDown(seedAdminMailKey)) StartCoroutine(CoSeedAdminMail());
            if (Input.GetKeyDown(refreshKey)) StartCoroutine(CoRefresh());
            if (Input.GetKeyDown(markFirstReadKey)) StartCoroutine(CoMarkFirstRead());
            if (Input.GetKeyDown(claimFirstKey)) StartCoroutine(CoClaimFirst());
            if (Input.GetKeyDown(deleteFirstKey)) StartCoroutine(CoDeleteFirst());
            if (Input.GetKeyDown(runFullFlowKey)) StartCoroutine(CoFullFlow());
        }

        private void EnsureMailClient()
        {
            if (MailClient.Instance != null) return;
            var go = new GameObject("MailClient");
            go.AddComponent<MailClient>();
            Debug.Log("[MailSample] Created MailClient at runtime — prefer Tools → Mail → Create MailClient and set URL in Inspector.");
        }

        private void ApplyJwt()
        {
            MailClient.Instance.Configure(serverBaseUrl, projectId, debug: true);
            var jwt = SampleJwt.CreateHs256(sampleUserId, jwtSecret);
            MailClient.Instance.SetJwt(jwt);
            Debug.Log($"[MailSample] JWT set for user={sampleUserId}");
        }

        [ContextMenu("F1 Seed Admin Mail")]
        public void SeedAdminMail() => StartCoroutine(CoSeedAdminMail());

        [ContextMenu("F2 Refresh Inbox")]
        public void RefreshInbox() => StartCoroutine(CoRefresh());

        [ContextMenu("F3 Mark First Read")]
        public void MarkFirstRead() => StartCoroutine(CoMarkFirstRead());

        [ContextMenu("F4 Claim First")]
        public void ClaimFirst() => StartCoroutine(CoClaimFirst());

        [ContextMenu("F5 Delete First")]
        public void DeleteFirst() => StartCoroutine(CoDeleteFirst());

        [ContextMenu("F6 Full Flow")]
        public void RunFullFlow() => StartCoroutine(CoFullFlow());

        private IEnumerator CoFullFlow()
        {
            _busy = true;
            Debug.Log("[MailSample] === FULL FLOW start ===");
            yield return CoSeedAdminMail();
            yield return CoRefresh();
            yield return CoMarkFirstRead();
            yield return CoClaimFirst();
            yield return CoRefresh();
            Debug.Log("[MailSample] === FULL FLOW done ===");
            _busy = false;
        }

        private IEnumerator CoSeedAdminMail()
        {
            _busy = true;
            var url = serverBaseUrl.TrimEnd('/') + "/api/v1/admin/mails";
            var title = "Sample compensation " + DateTime.UtcNow.ToString("HH:mm:ss");
            var body =
                "{"
                + "\"projectId\":\"" + EscapeJson(projectId) + "\","
                + "\"title\":\"" + EscapeJson(title) + "\","
                + "\"content\":\"Unity Samples~/InboxDemo seeded mail.\","
                + "\"attachments\":[{\"itemId\":\"gem\",\"count\":100}],"
                + "\"targetUserIds\":[\"" + EscapeJson(sampleUserId) + "\"],"
                + "\"senderName\":\"System\""
                + "}";

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Admin-Api-Key", adminApiKey);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogError($"[MailSample] Admin seed failed {req.responseCode}: {req.error}\n{req.downloadHandler?.text}");
            else
                Debug.Log($"[MailSample] Admin seed OK {req.responseCode}: {req.downloadHandler.text}");
            _busy = false;
        }

        private IEnumerator CoRefresh()
        {
            _busy = true;
            ApplyJwt();
            var task = MailClient.Instance.RefreshAsync(includeClaimed: true);
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted)
            {
                Debug.LogError("[MailSample] Refresh failed: " + task.Exception?.GetBaseException().Message);
            }
            else
            {
                var list = MailClient.Instance.Inbox;
                Debug.Log($"[MailSample] Inbox count={list.Count}");
                for (var i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    var att = m.attachments != null ? m.attachments.Count : 0;
                    Debug.Log($"  [{i}] id={m.id} read={m.isRead} claimed={m.isClaimed} att={att} title={m.title}");
                }
            }
            _busy = false;
        }

        private IEnumerator CoMarkFirstRead()
        {
            _busy = true;
            if (!TryFirstId(out var id))
            {
                _busy = false;
                yield break;
            }
            var task = MailClient.Instance.MarkReadAsync(id);
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted)
                Debug.LogError("[MailSample] MarkRead failed: " + task.Exception?.GetBaseException().Message);
            else
                Debug.Log($"[MailSample] MarkRead OK id={id} isRead={task.Result?.isRead}");
            _busy = false;
        }

        private IEnumerator CoClaimFirst()
        {
            _busy = true;
            if (!TryFirstId(out var id))
            {
                _busy = false;
                yield break;
            }
            var task = MailClient.Instance.ClaimAsync(id);
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted)
            {
                Debug.LogError("[MailSample] Claim failed: " + task.Exception?.GetBaseException().Message);
            }
            else
            {
                var r = task.Result;
                Debug.Log($"[MailSample] Claim OK already={r.alreadyClaimed} claimedAt={r.claimedAt}");
                if (r.attachments != null)
                {
                    foreach (var a in r.attachments)
                        Debug.Log($"  grant itemId={a.itemId} count={a.count}");
                }
            }
            _busy = false;
        }

        private IEnumerator CoDeleteFirst()
        {
            _busy = true;
            if (!TryFirstId(out var id))
            {
                _busy = false;
                yield break;
            }
            var task = MailClient.Instance.DeleteAsync(id);
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted)
                Debug.LogError("[MailSample] Delete failed: " + task.Exception?.GetBaseException().Message);
            else
                Debug.Log($"[MailSample] Delete OK id={id} result={task.Result}");
            _busy = false;
        }

        private bool TryFirstId(out string id)
        {
            id = null;
            var list = MailClient.Instance?.Inbox;
            if (list == null || list.Count == 0)
            {
                Debug.LogWarning("[MailSample] Inbox empty — press F1 then F2 first.");
                return false;
            }
            id = list[0].id;
            return !string.IsNullOrEmpty(id);
        }

        private static string EscapeJson(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
