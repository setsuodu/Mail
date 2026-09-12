using System;
using System.Text;
using System.Threading.Tasks;
using Setsuodu.Mail.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace Setsuodu.Mail.Transport
{
    public sealed class MailApiClient
    {
        private readonly string _baseUrl;
        private string _jwt;
        private readonly bool _debug;

        public MailApiClient(string baseUrl, bool debugHttp = false)
        {
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _debug = debugHttp;
        }

        public void SetJwt(string jwt) => _jwt = jwt;

        public async Task<InboxListResponse> ListInboxAsync(string projectId = null, bool includeClaimed = true, int page = 1, int pageSize = 50)
        {
            var q = $"includeClaimed={includeClaimed}&page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(projectId))
                q += $"&projectId={UnityWebRequest.EscapeURL(projectId)}";
            return await GetAsync<InboxListResponse>($"/api/v1/inbox?{q}");
        }

        public async Task<MailItem> MarkReadAsync(string userMailId, string projectId = null)
        {
            var q = string.IsNullOrEmpty(projectId) ? "" : $"?projectId={UnityWebRequest.EscapeURL(projectId)}";
            return await PostAsync<MailItem>($"/api/v1/inbox/{userMailId}/read{q}", null);
        }

        public async Task<ClaimResponse> ClaimAsync(string userMailId, string projectId = null)
        {
            var q = string.IsNullOrEmpty(projectId) ? "" : $"?projectId={UnityWebRequest.EscapeURL(projectId)}";
            return await PostAsync<ClaimResponse>($"/api/v1/inbox/{userMailId}/claim{q}", null);
        }

        public async Task<bool> DeleteAsync(string userMailId, string projectId = null)
        {
            var q = string.IsNullOrEmpty(projectId) ? "" : $"?projectId={UnityWebRequest.EscapeURL(projectId)}";
            var url = $"{_baseUrl}/api/v1/inbox/{userMailId}{q}";
            using var req = UnityWebRequest.Delete(url);
            ApplyAuth(req);
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();
            if (_debug)
                Debug.Log($"[Mail] DELETE {url} → {req.responseCode}");
            return req.responseCode is 204 or 200;
        }

        private async Task<T> GetAsync<T>(string path) where T : class
        {
            var url = _baseUrl + path;
            using var req = UnityWebRequest.Get(url);
            ApplyAuth(req);
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();
            if (_debug)
                Debug.Log($"[Mail] GET {url} → {req.responseCode} {req.downloadHandler?.text}");
            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"Mail GET failed: {req.responseCode} {req.error}");
            return JsonUtility.FromJson<T>(req.downloadHandler.text);
        }

        private async Task<T> PostAsync<T>(string path, string jsonBody) where T : class
        {
            var url = _baseUrl + path;
            var body = string.IsNullOrEmpty(jsonBody) ? Encoding.UTF8.GetBytes("{}") : Encoding.UTF8.GetBytes(jsonBody);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyAuth(req);
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();
            if (_debug)
                Debug.Log($"[Mail] POST {url} → {req.responseCode} {req.downloadHandler?.text}");
            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"Mail POST failed: {req.responseCode} {req.error}");
            return JsonUtility.FromJson<T>(req.downloadHandler.text);
        }

        private void ApplyAuth(UnityWebRequest req)
        {
            if (!string.IsNullOrEmpty(_jwt))
                req.SetRequestHeader("Authorization", "Bearer " + _jwt);
        }
    }
}
