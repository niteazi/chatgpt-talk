using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.Runtime;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;


public class textToSpeech : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    async void Start()
    {
        try
        {
            var credentials = new BasicAWSCredentials("AKIA4MTWLCAZQNJ2Z25I", "qVKfKo4U+5b5o7BIVPuDejFD/yiJgsDHF1/Uw0tA");


            using (var client = new AmazonPollyClient(credentials, Amazon.RegionEndpoint.EUCentral1))
            {
                var request = new SynthesizeSpeechRequest()
                {
                    Text = "Hello world",
                    Engine = Engine.Neural,
                    VoiceId = VoiceId.Gregory,
                    OutputFormat = OutputFormat.Mp3
                };

                var response = await client.SynthesizeSpeechAsync(request);

                Debug.Log($"AWS Polly Response Status: {response.HttpStatusCode}");

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    var filePath = Path.Combine(Application.persistentDataPath, "audio.mp3");
                    Debug.Log("File Path: " + filePath);

                    WriteIntoFile(response.AudioStream, filePath);

                    // Load the AudioClip directly from the saved file
                    var clip = LoadAudioClip(filePath);

                    Debug.Log($"Clip Length: {clip.length}");
                    Debug.Log($"Clip Sample Rate: {clip.frequency}");

                    // Play the loaded AudioClip using the AudioSource
                    audioSource.clip = clip;
                    audioSource.Play();
                }
                else
                {
                    Debug.LogError($"Error: {response.HttpStatusCode}");
                }
            }
        }
        catch (AmazonPollyException pollyEx)
        {
            Debug.LogError($"Polly Request Error: {pollyEx.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Unexpected Error: {ex.Message}");
        }
    }

    private void WriteIntoFile(Stream stream, string filePath)
{
    using (var fileStream = File.Create(filePath))
    {
        stream.CopyTo(fileStream);
    }
}


    private AudioClip LoadAudioClip(string filePath)
    {
        var www = UnityWebRequestMultimedia.GetAudioClip($"file://{filePath}", AudioType.MPEG);

        // Send the request and wait for it to complete
        www.SendWebRequest();
        while (!www.isDone) { }

        // Log load status
        Debug.Log($"AudioClip Load Status: {www.result}");

        return DownloadHandlerAudioClip.GetContent(www);
    }


}
