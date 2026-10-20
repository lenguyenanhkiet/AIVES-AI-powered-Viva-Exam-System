using System;
using System.Collections.Generic;
using System.Text;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Topics;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AIpoweredVivaExamSystem.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<AIpoweredVivaExamSystem.Application.Authentication.LoginService>();
            services.AddScoped<IRubricService, RubricService>();
            services.AddScoped<ISubjectService, SubjectService>();
            services.AddScoped<ITopicService, TopicService>();
            services.AddScoped<IQuestionService, QuestionService>();
            services.AddScoped<QuestionBank.QuestionImportService>();
            services.AddScoped<QuestionBank.DocumentService>();
            services.AddScoped<Scheduling.ExamScheduleService>();
            services.AddScoped<Exams.IExamScheduleService>(p => p.GetRequiredService<Scheduling.ExamScheduleService>());
            services.AddScoped<Scheduling.IScheduleReader>(p => p.GetRequiredService<Scheduling.ExamScheduleService>());
            services.AddScoped<Scheduling.IScheduleReadiness>(p => p.GetRequiredService<Scheduling.ExamScheduleService>());
            services.AddScoped<Authentication.AccessService>();
            services.AddScoped<Exams.ExamAccessGuard>();
            services.AddScoped<Exams.ExamFoundationService>();
            services.AddScoped<Exams.ExamPoolService>();
            services.AddScoped<Exams.IExamPoolReader>(p => p.GetRequiredService<Exams.ExamPoolService>());
            services.AddScoped<ExamStudents.ExamRosterService>();
            services.AddScoped<Exams.IExamRosterService>(p => p.GetRequiredService<ExamStudents.ExamRosterService>());
            services.AddScoped<ExamStudents.IExamRosterReader>(p => p.GetRequiredService<ExamStudents.ExamRosterService>());
            services.AddScoped<Exams.IVivaSessionService, VivaSessions.VivaSessionService>();
            services.AddValidatorsFromAssemblyContaining<CreateRubricRequestValidator>();
            return services;
        }
    }
}
