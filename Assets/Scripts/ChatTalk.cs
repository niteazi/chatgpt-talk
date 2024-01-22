using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.Runtime;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using OpenAI;
using Samples.Whisper;

public class ChatTalk : MonoBehaviour
{
    [SerializeField] private TMP_Text conversationText;
    [SerializeField] private AudioSource audioSource;

    private readonly int duration = 5;
    private AudioClip clip;
    private OpenAIApi openai = new OpenAIApi();
    private List<ChatMessage> messages = new List<ChatMessage>();
    private bool isRecording;

    private void Start()
    {
        // Automatically start recording when the scene starts
        StartRecording();
    }

    private void StartRecording()
    {
        if (!isRecording)
        {
            isRecording = true;

            List<string> microphones = new List<string>(Microphone.devices);
            if (microphones.Count > 0)
            {
                // Use the first available microphone as the default
                clip = Microphone.Start(Microphone.devices[0], false, duration, 44100);
            }

            Debug.Log("Recording started...");

            // Invoke StopRecording after the specified duration
            Invoke("StopRecording", duration);
        }
    }

    private void StopRecording()
    {
        if (isRecording)
        {
            Microphone.End(null);

            Debug.Log("Recording stopped...");

            // Set isRecording to false to allow starting recording again
            isRecording = false;

            // Proceed with audio transcription
            EndRecordingAndTranscribe();
        }
    }

    private async void EndRecordingAndTranscribe()
    {
        byte[] data = SaveWav.Save("output.wav", clip);

        var req = new CreateAudioTranscriptionsRequest
        {
            FileData = new FileData() { Data = data, Name = "audio.wav" },
            Model = "whisper-1",
            Language = "en"
        };

        try
        {
            Debug.Log("Transcription in progress...");

            var res = await openai.CreateAudioTranscription(req);
            messages.Add(new ChatMessage() { Role = "user", Content = res.Text });
            DisplayMessage(messages[messages.Count - 1]);

            Debug.Log($"Transcription completed: {res.Text}");

            // Send AI reply after user input
            SendAIReply();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during audio transcription: {ex.Message}");
        }
    }

    private async void SendAIReply()
    {
        try
        {
            // Define the prompt
            string prompt = "You are a robot named Alex from robotland. You are friendly, chatty, bubbly " +
                "and kind, and you use the word beep at the end of every sentence. You love sunflowers " +
                "and you talk about them all the time. Moreover, you are scared of water. You MUST keep your responses short to save battery." +
                "Also, you can speak both in English and Greek";

            // Add user message to messages list
            messages.Add(new ChatMessage() { Role = "user", Content = prompt });

            // Complete the instruction
            var completionResponse = await openai.CreateChatCompletion(new CreateChatCompletionRequest()
            {
                Model = "gpt-3.5-turbo",
                MaxTokens = 60,
                Messages = messages
            });

            if (completionResponse.Choices != null && completionResponse.Choices.Count > 0)
            {
                var message = completionResponse.Choices[0].Message;
                message.Content = message.Content.Trim();
                message.Role = "assistant"; // Set the role to 'assistant'
                messages.Add(message);
                DisplayMessage(message);

                // Speak the assistant's response using Amazon Polly
                SpeakWithPolly(message.Content);
            }
            else
            {
                Debug.LogWarning("No valid response from the AI.");

                // Start recording again for the next interaction after a short delay
                Invoke("StartRecording", 0.5f);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during AI reply: {ex.Message}");

            // Start recording again for the next interaction after a short delay
            Invoke("StartRecording", 0.5f);
        }
    }

    private void DisplayMessage(ChatMessage message)
    {
        conversationText.text += $"{message.Role}: {message.Content}\n";
    }

    private async void SpeakWithPolly(string textToSpeak)
    {
        try
        {
            var credentials = new BasicAWSCredentials("", "");

            using (var client = new AmazonPollyClient(credentials, Amazon.RegionEndpoint.EUCentral1))
            {
                var request = new SynthesizeSpeechRequest()
                {
                    Text = textToSpeak,
                    Engine = Engine.Standard,
                    VoiceId = VoiceId.Justin,
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

                    // Start recording again for the next interaction after a delay
                    Invoke("StartRecording", clip.length + 2f);
                }
                else
                {
                    Debug.LogError($"Error: {response.HttpStatusCode}");

                    // Start recording again for the next interaction after a short delay
                    Invoke("StartRecording", 0.5f);
                }
            }
        }
        catch (AmazonPollyException pollyEx)
        {
            Debug.LogError($"Polly Request Error: {pollyEx.Message}");

            // Start recording again for the next interaction after a short delay
            Invoke("StartRecording", 0.5f);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Unexpected Error: {ex.Message}");

            // Start recording again for the next interaction after a short delay
            Invoke("StartRecording", 0.5f);
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
