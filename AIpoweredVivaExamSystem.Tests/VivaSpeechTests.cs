using System.Net;
using System.Text.Json;
using AIpoweredVivaExamSystem.Business.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Enums;
using AIpoweredVivaExamSystem.Business.Integrations.VivaSessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class VivaSpeechTests
{
    private sealed class Provider : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        public int Calls;
        public TaskCompletionSource NextStarted {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseNext {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            if(body.RootElement.GetProperty("text").GetString()=="Next main question")
            {
                NextStarted.TrySetResult();await ReleaseNext.Task.WaitAsync(ct);
            }
            var content=new ByteArrayContent([1,2,3]);content.Headers.ContentType=new("audio/mpeg");
            return new(HttpStatusCode.OK){Content=content};
        }
    }
    private sealed class Interview : IVivaInterviewService
    {
        private readonly Guid sessionId=Guid.NewGuid(),turnId=Guid.NewGuid(),questionId=Guid.NewGuid();
        public int Position=1;
        public VivaInterviewState State()=>new(sessionId,Guid.NewGuid(),"Exam","PRN222","Subject",DateTimeOffset.UtcNow,null,
            DateTimeOffset.UtcNow.AddMinutes(20),1200,300,300,Position,2,0,1,
            new(turnId,questionId,Position,AnswerType.Main,0,Position==1?"Current question":"Next main question",null,DateTimeOffset.UtcNow,null,false),
            [new(turnId,questionId,Position,AnswerType.Main,0,Position==1?"Current question":"Next main question",null,DateTimeOffset.UtcNow,null,false)],
            [new(Guid.NewGuid(),1,"Current question"),new(Guid.NewGuid(),2,"Next main question")]);
        public Task<VivaInterviewState> GetStateAsync(Guid id,CancellationToken ct)=>Task.FromResult(State());
        public Task<VivaInterviewState> OpenOrResumeByEnrollmentAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<VivaInterviewState> SubmitAnswerAsync(SubmitAnswerRequest r,CancellationToken ct)=>throw new NotSupportedException();
        public Task<string> TranscribeAudioAsync(Guid id,byte[] bytes,string file,string lang,CancellationToken ct)=>throw new NotSupportedException();
    }
    [Fact]
    public async Task Next_main_speech_warms_without_blocking_current_audio_and_playback_shares_the_paid_request()
    {
        using var provider=new Provider();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ElevenLabs:ApiKey"]="test-key"}).Build();
        using var speech=new ElevenLabsSpeechSynthesizer(provider,config,NullLogger<ElevenLabsSpeechSynthesizer>.Instance);
        var interview=new Interview();var service=new VivaQuestionSpeechService(interview,speech);var state=interview.State();
        try
        {
            Assert.Equal(new byte[]{1,2,3},await service.SpeakAsync(state.SessionId,state.ActiveTurn!.TurnId,default));
            await provider.NextStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            interview.Position=2;
            var playback=service.SpeakAsync(state.SessionId,state.ActiveTurn.TurnId,default);
            provider.ReleaseNext.TrySetResult();
            Assert.Equal(new byte[]{1,2,3},await playback);
            Assert.Equal(2,provider.Calls); // one synthesis per prompt, including the concurrent playback
        }
        finally {provider.ReleaseNext.TrySetResult();}
    }
}
