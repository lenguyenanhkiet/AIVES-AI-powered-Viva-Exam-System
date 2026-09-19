using System;
using System.Collections.Generic;
using System.Text;

namespace AIpoweredVivaExamSystem.Domain.Common;
/// <summary>
/// Represents an entity that tracks creation, modification, and deletion information. This class extends the BaseEntity class and adds properties for auditing purposes, such as CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, and DeletedAt. It is intended to be used as a base class for entities that require auditing capabilities in the domain model.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
