﻿using System.Text;
using System.Text.Json;
using informaticsge.Dto.Response;
using informaticsge.models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace informaticsge.RabbitMQ;

public class RabbitMqService
{
    private readonly IConnectionFactory _connectionFactory;
    private IConnection _connection;
    private IModel _channel;

    public RabbitMqService()
    {
        _connectionFactory = new ConnectionFactory()
        {
            HostName = "localhost",
            UserName = "guest",
            Password = "guest",
        };
        _connection = _connectionFactory.CreateConnection();

        _channel = _connection.CreateModel();
        
        _channel.QueueDeclare(queue: "requestQueue",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        
        _channel.QueueDeclare(queue: "resultQueue",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }
    
    
    public void SendRequest(SubmissionRequestDto request)
    {
        if (_channel == null)
        {
            throw new InvalidOperationException("Channel is not initialized.");
        }

        var jsonTask = JsonSerializer.Serialize(request);
        var body = Encoding.UTF8.GetBytes(jsonTask);
        
        _channel.BasicPublish(exchange: "" , routingKey: "requestQueue", basicProperties: null, body : body);
    }
    

    public void ReceiveResult(Action<string> processResult)
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            processResult(message);
        };
        _channel.BasicConsume(queue: "resultQueue", autoAck: true, consumer: consumer);
    }
    
    public void Close()
    {
        _channel.Close();
        _connection.Close();
    }
    
    
}