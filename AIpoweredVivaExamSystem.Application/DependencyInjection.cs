using System;
using System.Collections.Generic;
using System.Text;
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
            services.AddScoped<IRubricService, RubricService>();
            services.AddScoped<ISubjectService, SubjectService>();
            services.AddScoped<ITopicService, TopicService>();
            services.AddValidatorsFromAssemblyContaining<CreateRubricRequestValidator>();
            return services;
        }
    }
}
