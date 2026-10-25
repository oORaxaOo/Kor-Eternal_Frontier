using KorTheBlight.Simulation;
namespace KorTheBlight.Tests;
internal static class ResidentLifeProfileChecks
{
 public static void Run()
 {
  var p=ResidentLifeProfile.Create(17,"Mira");var same=ResidentLifeProfile.Create(17,"Mira");
  Check(p.Traits.SequenceEqual(same.Traits)&&p.ScheduleOffsetHours==same.ScheduleOffsetHours,"traits and schedules deterministic by resident identity");
  Check(p.Traits.Count==2&&p.Traits.Distinct().Count()==2&&!(p.HasTrait(ResidentTrait.Sociable)&&p.HasTrait(ResidentTrait.Solitary)),"two compatible unique traits");
  Check(Enumerable.Range(0,24).Any(hour=>p.ActivityAtTick(hour*ResidentLifeProfile.TicksPerHour)==ResidentDailyActivity.Work),"daily schedule includes a work block");
  Check(p.ActivityAtTick(2*ResidentLifeProfile.TicksPerHour)==ResidentDailyActivity.Sleep,"daily schedule includes sleep hours");
  Check(p.ActivityAtTick(ResidentLifeProfile.TicksPerDay+10)==p.ActivityAtTick(10),"schedule repeats on simulated day boundaries");
  p.RecordMemory(100,ResidentMemoryKind.WorkCompleted,0,4,"Completed a task.");
  p.RecordMemory(100,ResidentMemoryKind.Friendship,18,3,"Formed a meaningful bond.");
  Check(p.RecentMoodBias(100)>0&&p.RecentMoodBias(18100)==0,"memory influence decays deterministically");
  Check(ResidentLifeProfile.FromTraits([ResidentTrait.Industrious,ResidentTrait.Careful]).WorkSpeedMultiplier(0,0)>1&&ResidentLifeProfile.FromTraits([ResidentTrait.Anxious,ResidentTrait.Solitary]).WorkSpeedMultiplier(0,75)<1,"traits affect actual work rates");
  for(int i=0;i<ResidentLifeProfile.MaximumMemories+8;i++)p.RecordMemory(200+i,ResidentMemoryKind.WorkCompleted,0,1,$"Work event {i}.");
  Check(p.Memories.Count==64&&p.Memories[0].Detail=="Work event 8.","memory history is bounded");
  var snapshot=p.CaptureSnapshot();Check(snapshot.IsValid(17),"saved life snapshot valid");
  var restored=ResidentLifeProfile.Create(17,"Mira");restored.RestoreSnapshot(snapshot,17);
  Check(restored.Traits.SequenceEqual(p.Traits)&&restored.ScheduleOffsetHours==p.ScheduleOffsetHours&&restored.Memories.SequenceEqual(p.Memories)&&Math.Abs(restored.RecentMoodBias(300)-p.RecentMoodBias(300))<.001f,"round-trip preserves life data and schedule");
  var invalid=p.CaptureSnapshot();invalid.Traits[1]=invalid.Traits[0];Check(!invalid.IsValid(17),"invalid duplicate traits rejected");
 }
 private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException($"Resident life check failed: {message}");}
}
