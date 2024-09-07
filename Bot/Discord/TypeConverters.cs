using Discord.Interactions;

namespace Hyperstellar.Discord;

// Util class for other type converters in this package
internal static class TypeConverters
{
    // Shorthand for generating an error response
    internal static Task<TypeConverterResult> Error(string reason) => Task.FromResult(TypeConverterResult.FromError(InteractionCommandError.ConvertFailed, reason));

    // Shorthand for generating a successful conversion
    internal static Task<TypeConverterResult> Success(object value) => Task.FromResult(TypeConverterResult.FromSuccess(value));
}
