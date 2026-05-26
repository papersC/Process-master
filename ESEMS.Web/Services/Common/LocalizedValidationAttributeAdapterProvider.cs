using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.Extensions.Localization;

namespace ESEMS.Web.Services.Common;

/// <summary>
/// Routes default validation messages through SharedResource so [Required],
/// [StringLength], [Range], [EmailAddress] etc. respect the current UI culture
/// without each attribute needing an explicit ErrorMessage.
///
/// Why this exists: ASP.NET Core's DataAnnotationsLocalization only consults
/// DataAnnotationLocalizerProvider when ErrorMessage is explicitly set on the
/// attribute. Default-message attributes (the common case) skip localization
/// entirely. This provider intercepts each adapter resolution, sees an empty
/// ErrorMessage, and injects the framework's default message TEMPLATE as the
/// ErrorMessage — which then becomes the lookup key inside SharedResource.{ar}.resx.
/// </summary>
public sealed class LocalizedValidationAttributeAdapterProvider : IValidationAttributeAdapterProvider
{
    private readonly ValidationAttributeAdapterProvider _baseline = new();

    public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer)
    {
        if (string.IsNullOrEmpty(attribute.ErrorMessage)
            && string.IsNullOrEmpty(attribute.ErrorMessageResourceName))
        {
            // Inject the default message template as the ErrorMessage so the
            // baseline adapter routes it through the localizer. Keys in the
            // .resx files match these strings verbatim.
            attribute.ErrorMessage = attribute switch
            {
                RequiredAttribute     => "The {0} field is required.",
                StringLengthAttribute => "The field {0} must be a string with a maximum length of {1}.",
                MaxLengthAttribute    => "The field {0} must be a string or array type with a maximum length of '{1}'.",
                MinLengthAttribute    => "The field {0} must be a string or array type with a minimum length of '{1}'.",
                RangeAttribute        => "The field {0} must be between {1} and {2}.",
                EmailAddressAttribute => "The {0} field is not a valid e-mail address.",
                _                     => attribute.ErrorMessage
            };
        }

        return _baseline.GetAttributeAdapter(attribute, stringLocalizer);
    }
}
