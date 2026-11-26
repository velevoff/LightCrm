namespace LightCrm.Models;

// Запланована дія (її реалізують Call і Meeting).
public interface IScheduledAction {
    DateTime ScheduledAt { get; set; }
    bool IsCompleted { get; set; }

    // Дія прострочена: час минув, а виконаною її не позначено.
    bool IsOverdue(DateTime now);

    // Перенести дію на новий час (скидає ознаку виконання).
    void Reschedule(DateTime newTime);
}
