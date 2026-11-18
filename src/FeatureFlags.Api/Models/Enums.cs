namespace FeatureFlags.Api.Models;

public enum FlagType { Boolean, String, Number }
public enum RuleType { Percentage, Attribute }
public enum RuleOperator { Equals, NotEquals, InList }
public enum ChangeRequestStatus { Pending, Approved, Rejected }
