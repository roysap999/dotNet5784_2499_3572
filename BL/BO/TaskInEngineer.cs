﻿

namespace BO;
/// <summary>
/// 
/// </summary>
public class TaskInEngineer
{
   public  int Id { get; init; }
   public string? Alias { get; init; }
   public override string ToString() => this.ToStringProperty();
}
