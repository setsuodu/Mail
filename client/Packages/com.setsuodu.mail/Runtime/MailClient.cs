using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Setsuodu.Mail.Models;
using Setsuodu.Mail.Transport;
using UnityEngine;

namespace Setsuodu.Mail
{
    /// <summary>
    /// In-game mail client. Set JWT after login (from MP), then Refresh / Claim / etc.
    /// </summary>
    public sealed class MailClient : MonoBehaviour
    {
        [SerializeField] private string serverBaseUrl = "http://localhost:12081";
        [SerializeField] private string projectId = "default";
        [SerializeField] private bool debugHttp = true;

        private MailApiClient _api;
        private List<MailItem> _inbox = new();

        public static MailClient Instance { get; private set; }

        public IReadOnlyList<MailItem> Inbox => _inbox;
        public event Action InboxChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Mail] Duplicate MailClient removed — keep one only.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _api = new MailApiClient(serverBaseUrl, debugHttp);
            Debug.Log($"[Mail] up  url={serverBaseUrl}  project={projectId}");
        }

        /// <summary>Optional: override URL / project after Awake (sample / runtime).</summary>
        public void Configure(string baseUrl, string project, bool debug = true)
        {
            if (!string.IsNullOrEmpty(baseUrl))
                serverBaseUrl = baseUrl.TrimEnd('/');
            if (!string.IsNullOrEmpty(project))
                projectId = project;
            debugHttp = debug;
            _api = new MailApiClient(serverBaseUrl, debugHttp);
        }

        /// <summary>Call after MP login with the player JWT.</summary>
        public void SetJwt(string jwt)
        {
            if (_api == null)
                _api = new MailApiClient(serverBaseUrl, debugHttp);
            _api.SetJwt(jwt);
        }

        public async Task RefreshAsync(bool includeClaimed = true)
        {
            var resp = await _api.ListInboxAsync(projectId, includeClaimed);
            _inbox = resp?.items ?? new List<MailItem>();
            InboxChanged?.Invoke();
        }

        public async Task<MailItem> MarkReadAsync(string userMailId)
        {
            var item = await _api.MarkReadAsync(userMailId, projectId);
            ReplaceLocal(item);
            InboxChanged?.Invoke();
            return item;
        }

        /// <summary>
        /// Claim attachments. Game logic should grant items based on returned attachments
        /// (idempotent: alreadyClaimed=true still returns the same attachment list).
        /// </summary>
        public async Task<ClaimResponse> ClaimAsync(string userMailId)
        {
            var resp = await _api.ClaimAsync(userMailId, projectId);
            if (resp != null)
            {
                var local = _inbox.Find(m => m.id == userMailId);
                if (local != null)
                {
                    local.isClaimed = true;
                    local.isRead = true;
                    local.claimedAt = resp.claimedAt;
                }
                InboxChanged?.Invoke();
            }
            return resp;
        }

        public async Task<bool> DeleteAsync(string userMailId)
        {
            var ok = await _api.DeleteAsync(userMailId, projectId);
            if (ok)
            {
                _inbox.RemoveAll(m => m.id == userMailId);
                InboxChanged?.Invoke();
            }
            return ok;
        }

        private void ReplaceLocal(MailItem item)
        {
            if (item == null) return;
            var idx = _inbox.FindIndex(m => m.id == item.id);
            if (idx >= 0)
                _inbox[idx] = item;
        }
    }
}
