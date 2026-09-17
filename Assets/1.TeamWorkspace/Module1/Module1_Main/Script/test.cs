using UnityEngine;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;

public class test1 : MonoBehaviour
{
    [Header("Network Settings")]
    public string targetIpAddress = "127.0.0.1"; // IP ของเครื่องที่รัน GAMA
    public int port = 9876;
    public int ID = 1;
    private UdpClient client;
    private IPEndPoint remoteEndPoint;
    private string localIP;

    void Start()
    {
        client = new UdpClient();
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(targetIpAddress), port);

        // ดึง IP เครื่องตัวเองมาเก็บไว้ก่อน
        localIP = GetLocalIPAddress();
        Debug.Log($"UDP Sender พร้อม! IP เครื่องนี้คือ: {localIP}");
    }

    void Update()
    {
        Vector3 pos = transform.position;

        // ส่ง IP พ่วงไปกับข้อมูลตำแหน่ง (Format: IP;X;Y;Z)
        string message = $"{localIP};{pos.x:F2};{pos.y:F2};{pos.z:F2};{ID}";

        SendData(message);
    }

    public void SendData(string message)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            
            client.Send(data, data.Length, remoteEndPoint);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error: {e.Message}");
        }
    }

    // ฟังก์ชันช่วยดึง IP ของเครื่องที่กำลังรัน Unity
    private string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1"; // Default หากหาไม่เจอ
    }

    void OnApplicationQuit()
    {
        if (client != null) client.Close();
    }
}