using Microsoft.AspNetCore.Http.HttpResults;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace WppConnect4Aspnet.Models
{
    public class SendMessageRequest
    {
        public required string To { get; set; }
        public required string Message { get; set; }
    }
    public class MessageResponse
    {
        public bool status { get; set; }
        public Message message { get; set; }
    }
    public class Message
    {
        public string id { get; set; }
        public int ack { get; set; }
        public string? from { get; set; }
        public string? to { get; set; }
        public long latestEditMsgKey { get; set; }
        [JsonPropertyName("sendMsgResult")]
        public SendMsgResultDetails sendMsgResult { get; set; }
    }
    public class DeleteMessageResult
    {
        [JsonPropertyName("id")]
        public string id { get; set; }
        [JsonPropertyName("sendMsgResult")]
        public SendMsgResultDeleted sendMsgResult { get; set; }
        [JsonPropertyName("isRevoked")]
        public bool isRevoked { get; set; }
        [JsonPropertyName("isDeleted")]
        public bool isDeleted { get; set; }
        [JsonPropertyName("isSentByMe")]
        public bool isSentByMe { get; set; }
    }
    public class SendMsgResultDetails
    {

    }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SendMsgResultDeleted
    {
        [EnumMember(Value = "OK")]
        OK,
        [EnumMember(Value = "ERROR_NETWORK")]
        ERROR_NETWORK,
        [EnumMember(Value = "ERROR_EXPIRED")]
        ERROR_EXPIRED,
        [EnumMember(Value = "ERROR_CANCELLED")]
        ERROR_CANCELLED,
        [EnumMember(Value = "ERROR_UPLOAD")]
        ERROR_UPLOAD,
        [EnumMember(Value = "ERROR_UNKNOWN")]
        ERROR_UNKNOWN,
    }
}
