using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;

public class NotificationModel
{
    [BsonId]
    public ObjectId Id { get; set; }
    [BsonElement("userId")]
    public ObjectId UserId { get; set; }
    [BsonElement("message")]
    public string Message { get; set; }
    [BsonElement("date")]
    public DateTime Date { get; set; }
    [BsonElement("read")]
    public bool Read { get; set; }
}
