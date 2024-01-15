using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using OpenAI;
using System;
using Samples.Whisper;

public class ChatWithVoiceInput : MonoBehaviour
{
    [SerializeField] private TMP_Text conversationText;

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
                "and kind and you use the word beep at the end of every sentence. You love sunflowers " +
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
            }
            else
            {
                Debug.LogWarning("No valid response from the AI.");
            }

            // Start recording again for the next interaction after a short delay
            Invoke("StartRecording", 1f);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during AI reply: {ex.Message}");
        }
    }

    private void DisplayMessage(ChatMessage message)
    {
        conversationText.text += $"{message.Role}: {message.Content}\n";
    }
}
