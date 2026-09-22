namespace CulinaryBlog.Application.Common.Exceptions;

/// <summary>
/// Application Error Code — nguồn duy nhất. Bảng đầy đủ ở docs/decisions.md (cuối file).
/// KHÔNG hardcode chuỗi tiếng Việt vào field "type" của RFC 7807 (NFR-USE-003).
/// </summary>
public static class ErrorCodes
{
    // Auth
    public const string AuthEmailExists = "AUTH_EMAIL_EXISTS";                       // 409
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";         // 401
    public const string AuthTokenExpired = "AUTH_TOKEN_EXPIRED";                     // 401
    public const string AuthTokenInvalid = "AUTH_TOKEN_INVALID";                     // 401
    public const string AuthRefreshTokenExpired = "AUTH_REFRESH_TOKEN_EXPIRED";      // 401
    public const string AuthRefreshTokenRevoked = "AUTH_REFRESH_TOKEN_REVOKED";      // 401
    public const string AuthGoogleTokenInvalid = "AUTH_GOOGLE_TOKEN_INVALID";        // 400
    public const string AuthAccountDisabled = "AUTH_ACCOUNT_DISABLED";               // 403
    public const string AuthAccountLocked = "AUTH_ACCOUNT_LOCKED";                   // 423 (D17)

    // Recipe — LƯU Ý: không có RECIPE_SLUG_EXISTS, slug trùng thì auto-suffix (D10)
    public const string RecipeNotFound = "RECIPE_NOT_FOUND";                         // 404
    public const string RecipePublishIncomplete = "RECIPE_PUBLISH_INCOMPLETE";       // 400 (D3)
    public const string RecipeForbidden = "RECIPE_FORBIDDEN";                        // 403
    public const string RecipeConcurrencyConflict = "RECIPE_CONCURRENCY_CONFLICT";   // 409 (D4)
    public const string RecipeImageNotFound = "RECIPE_IMAGE_NOT_FOUND";             // 404 (D27)
    public const string RecipePrimaryImageRequired = "RECIPE_PRIMARY_IMAGE_REQUIRED"; // 400 (D22)

    // Category
    public const string CategoryNotFound = "CATEGORY_NOT_FOUND";                     // 404
    public const string CategoryNameExists = "CATEGORY_NAME_EXISTS";                 // 409
    public const string CategoryDeleteHasRecipes = "CATEGORY_DELETE_HAS_RECIPES";    // 409

    // File
    public const string FileSizeExceeded = "FILE_SIZE_EXCEEDED";                     // 400
    public const string FileMimeInvalid = "FILE_MIME_INVALID";                       // 400

    // Common
    public const string ValidationError = "VALIDATION_ERROR";                        // 400 (D4)
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";                   // 429
}
