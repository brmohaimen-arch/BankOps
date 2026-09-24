namespace BankOps.Modules.Settings.Contracts;

public record SettingDto(
    string Namespace,
    string Key,
    string Value, // raw JSON text — callers deserialize to whatever shape they expect
    long Version,
    DateTimeOffset UpdatedAt,
    string UpdatedBy);

public enum UpsertOutcome
{
    Created,
    Updated,
    VersionConflict,
}

public record UpsertResult(UpsertOutcome Outcome, long? NewVersion);
