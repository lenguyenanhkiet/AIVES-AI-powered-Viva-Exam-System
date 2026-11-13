namespace AIpoweredVivaExamSystem.Application.Contracts;

// Values crossing the Application boundary. Domain enums stay internal to use cases and adapters.
public enum AcademicStatus { Active = 1, Inactive = 2 }
public enum BloomLevel { Remember = 1, Understand = 2, Apply = 3, Analyze = 4 }
public enum QuestionDifficulty { Easy = 1, Medium = 2, Hard = 3 }
public enum QuestionSourceType { Manual = 1, AiGenerated = 2, DocumentGenerated = 3, Imported = 4 }
public enum QuestionStatus { Draft = 1, Approved = 2, Rejected = 3, Archived = 4 }
public enum AnswerType { Main = 1, FollowUp = 2 }
public enum UserRole { STUDENT, LECTURER, ADMIN }
public enum UserStatus { Active = 1, Inactive = 2, Suspended = 3, Locked = 4 }
public enum ExamStatus { Draft, Published, Ongoing, Completed, Cancelled }
