using TaskFlow.DTOs;
using TaskFlow.Models;

namespace TaskFlow.Services;

public class TaskService
{
    private readonly FirebaseService _firebaseService;

    public TaskService(FirebaseService firebaseService)
    {
        _firebaseService = firebaseService;
    }

    public async Task<TaskItem> Create(TaskDto dto, string userId)
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid().ToString(),
            Title = dto.Title,
            PriorityLevel = dto.PriorityLevel,
            Notes = dto.Notes ?? string.Empty,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _firebaseService.GetCollection("Task")
            .Document(task.Id)
            .SetAsync(new Dictionary<string, object>
            {
                { "Id", task.Id },
                { "Title", task.Title },
                { "PriorityLevel", task.PriorityLevel },
                { "Notes", task.Notes },
                { "UserId", task.UserId },
                { "CreatedAt", task.CreatedAt }
            });

        return task;
    }

    public async Task<List<TaskItem>> GetByUser(string userId)
    {
        var snapshot = await _firebaseService.GetCollection("Task")
            .WhereEqualTo("UserId", userId)
            .GetSnapshotAsync();

        var tasks = new List<TaskItem>();

        foreach (var doc in snapshot.Documents)
        {
            tasks.Add(MapToTaskItem(doc.ToDictionary()));
        }
        
        return tasks.OrderByDescending(t => t.CreatedAt).ToList();
    }

    public async Task<bool> Delete(string id, string userId)
    {
        var docRef = _firebaseService.GetCollection("Task").Document(id);
        var snapshot = await docRef.GetSnapshotAsync();

        if (!snapshot.Exists)
            return false;

        var data = snapshot.ToDictionary();

        var ownerId = data["UserId"].ToString();
        if (ownerId != userId)
            throw new UnauthorizedAccessException("Esta tarea no pertenece al usuario");

        await docRef.DeleteAsync();
        return true;
    }

    private static TaskItem MapToTaskItem(Dictionary<string, object> data)
    {
        return new TaskItem
        {
            Id = data["Id"].ToString()!,
            Title = data["Title"].ToString()!,
            PriorityLevel = Convert.ToInt32(data["PriorityLevel"]),
            Notes = data.TryGetValue("Notes", out var notes) ? notes.ToString()! : string.Empty,
            UserId = data["UserId"].ToString()!,
            CreatedAt = ((Google.Cloud.Firestore.Timestamp)data["CreatedAt"]).ToDateTime()
        };
    }
}
