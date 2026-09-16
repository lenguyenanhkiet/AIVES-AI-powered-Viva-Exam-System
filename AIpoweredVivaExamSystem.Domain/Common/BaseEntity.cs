using System;
using System.Collections.Generic;
using System.Text;

namespace AIpoweredVivaExamSystem.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}
