using System;
using System.Text.Json.Serialization;

namespace VRCGroupSwitcher.Models
{
    public class VRCUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("userIcon")]
        public string UserIcon { get; set; } = string.Empty;

        [JsonPropertyName("currentAvatarThumbnailImageUrl")]
        public string CurrentAvatarThumbnailImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "active";

        [JsonPropertyName("statusDescription")]
        public string StatusDescription { get; set; } = string.Empty;

        public string AvatarDisplayUrl => !string.IsNullOrEmpty(UserIcon) 
            ? UserIcon 
            : (!string.IsNullOrEmpty(CurrentAvatarThumbnailImageUrl) ? CurrentAvatarThumbnailImageUrl : "");
    }
}
