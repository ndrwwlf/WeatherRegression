using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using WeatherService.Db;
using WeatherService.Dto;

namespace WeatherService.Validation
{
    public class RequiredBindingMetadataProvider : IBindingMetadataProvider
    {
        public void CreateBindingMetadata(BindingMetadataProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Key.Name == null && context.Key.ModelType == typeof(PutPostLocation))
            {
                context.BindingMetadata.BindingSource = BindingSource.Body;
            }
        }

        public void GetBindingMetadata(BindingMetadataProviderContext context)
        {
            if (context.PropertyAttributes.OfType<RequiredAttribute>().Any())
            {
                context.BindingMetadata.IsBindingRequired = true;
            }
        }
    }
}
