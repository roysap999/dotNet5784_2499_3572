﻿

namespace BO;
/// <summary>
/// s
/// </summary>
public class MilestoneInList
{
   public int Id { get; init; }
    public string? Description { get; set; }
    public string?Alias { get; set; }
    public BO.Status Status { get; set; }
    public double CompletionPercentage { get; set; }
    //public override string ToString() => this.ToStringProperty();
}
