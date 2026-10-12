using System;
using System.Text.Json.Serialization;

namespace VRCGroupSwitcher.Models
{
    public class VRCGroup
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("groupId")]
        public string GroupIdField { get; set; } = string.Empty;

        // Group ID helper: /users/{userId}/groups returns "groupId": "grp_..." and "id": "gmem_..."
        public string GroupId
        {
            get
            {
                if (!string.IsNullOrEmpty(GroupIdField) && GroupIdField.StartsWith("grp_", StringComparison.OrdinalIgnoreCase))
                {
                    return GroupIdField;
                }
                if (!string.IsNullOrEmpty(Id) && Id.StartsWith("grp_", StringComparison.OrdinalIgnoreCase))
                {
                    return Id;
                }
                return !string.IsNullOrEmpty(GroupIdField) ? GroupIdField : Id;
            }
        }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("shortCode")]
        public string ShortCode { get; set; } = string.Empty;

        [JsonPropertyName("discriminator")]
        public string Discriminator { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("iconUrl")]
        public string IconUrl { get; set; } = string.Empty;

        [JsonPropertyName("bannerUrl")]
        public string BannerUrl { get; set; } = string.Empty;

        [JsonPropertyName("memberCount")]
        public int MemberCount { get; set; }

        [JsonPropertyName("isRepresenting")]
        public bool IsRepresenting { get; set; }

        public string DisplayCode => !string.IsNullOrEmpty(ShortCode) ? $"{ShortCode}.{Discriminator}" : "";
    }
}
