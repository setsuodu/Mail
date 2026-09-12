using System;
using System.Collections.Generic;

namespace Setsuodu.Mail.Models
{
    [Serializable]
    public class MailAttachment
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    public class MailItem
    {
        public string id;
        public string mailId;
        public string title;
        public string content;
        public List<MailAttachment> attachments;
        public bool isRead;
        public bool isClaimed;
        public string expireAt;
        public string createdAt;
        public string claimedAt;
    }

    [Serializable]
    public class InboxListResponse
    {
        public List<MailItem> items;
        public int page;
        public int pageSize;
        public int total;
    }

    [Serializable]
    public class ClaimResponse
    {
        public string mailItemId;
        public bool alreadyClaimed;
        public List<MailAttachment> attachments;
        public string claimedAt;
    }
}
