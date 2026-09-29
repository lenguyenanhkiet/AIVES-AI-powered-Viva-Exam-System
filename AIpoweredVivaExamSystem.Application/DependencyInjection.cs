using System;
using System.Collections.Generic;
using System.Text;
using AIpoweredVivaExamSystem.Application.Rubrics;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AIpoweredVivaExamSystem.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IRubricService, RubricService>();
            services.AddValidatorsFromAssemblyContaining<CreateRubricRequestValidator>();
            return services;
        }
    }
}
