﻿


using BlApi;
using BO;

using DO;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace BlImplementation;

internal class TaskImplementation : ITask
{
    private readonly IBl _bl;
    internal TaskImplementation(IBl bl) => _bl = bl;


    private DalApi.IDal _dal = DalApi.Factory.Get;

    public bool Schedule_date()
    {
        return (_dal.Task.ReadAll(k => k.scheduledDate == null).Any());

    }


    public int Create(BO.Task item)
    {
        if (_bl.GetDate("StartDate") != DateTime.MinValue)
            throw new BlLogicalErrorException("It is not possible to create new tasks after the start of the project");



        if (item.Alias is null || item.Alias == "")
            throw new BlInvalidInputException("The data you entered is incorrect for the task");
        int EngineerId = 0;
        if (item.Engineer is not null)
        {
            if (check_id_engineer(item.Engineer.Id) is true && item.Engineer.Id != 0)
                throw new BlInvalidInputException("The data you entered is incorrect for the task's engineer");

            EngineerId = item.Engineer.Id;
        }


        if (item!.Dependencies is null)
            item.Dependencies = new List<BO.TaskInList>();

        DO.Task doTask = new DO.Task(item.Id, EngineerId, item.Alias, item.Deliverables, item.Description,
            item.Remarks, DateTime.Now, item.ScheduledDate, item.StartDate, item.CompleteDate,
             (DO.EngineerExperience?)item.Complexity, item.RequiredEffortTime);

        int idTask;
        try
        {
            idTask = _dal.Task.Create(doTask);
        }

        catch (DO.DalAlreadyExistsException ex)
        {
            throw new BO.BlAlreadyExistsException($"Student with ID={item.Id} already exists", ex);
        }

        if (item!.Dependencies is null)
            item.Dependencies = new List<BO.TaskInList>();

        return idTask;
    }

    public bool check_id_engineer(int id)
    {
        return !(_dal.Engineer.ReadAll(k => k.Id == id).Any());
    }


    public void CreateSchedule(DateTime date)
    {



        _bl.SetDate(_bl.Clock, "StartDate");
        foreach (var temp in _dal.Task.ReadAll())
        {
            Console.WriteLine("enter the scheduled date for the task");
            string s = Console.ReadLine()!;
            if (!DateTime.TryParse(s, out date))
                throw new BlInvalidInputException("The data you entered is incorrect for a date");
            if (!_dal.Dependency.ReadAll(p => p.DependentTask == temp.Id).Any())
            {
                if (_bl.GetDate("StartDate") > date)
                    throw new BlLogicalErrorException("The start date cannot be later than the project start date");
                DO.Task dotask = temp with { scheduledDate = date };
                _dal.Task.Update(dotask);
            }
            else
            {
                try
                {
                    UpdateDate(date, temp.Id);
                }
                catch (BlLogicalErrorException)
                {

                    throw;
                }

            }
        }



    }

    public void Delete(int id)
    {
        if (_bl.GetDate("StartDate") != DateTime.MinValue)
            throw new BlLogicalErrorException("It is not possible to delete  tasks after entering a start date for the project");



        if (_dal.Dependency.ReadAll(K => K.DependsOnTask == id).Any())
            throw new BlNullPropertyException("It is not possible to delete a task that depends on other tasks");
        try
        {
            _dal.Task.Delete(id);
        }
        catch (DO.DalDoesNotExistException ex)
        {
            throw new BO.BlDoesNotExistException($"Task with ID={id} does not exists", ex);
        }
    }

    public BO.Task? Read(int id)
    {
        DO.Task? doTask = _dal.Task.Read(id);
        if (doTask is null)
            return null;

        IEnumerable<Dependency> depList = _dal.Dependency.ReadAll(t => t.DependentTask == id);
        DO.Engineer eng = _dal.Engineer.Read(doTask.EngineerId) ?? new();

        if (depList is null)
            depList = new List<Dependency>();



        return new BO.Task()
        {
            Id = id,
            Alias = doTask.Alias,
            Description = doTask.Description,
            Deliverables = doTask.Deliverables,
            Remarks = doTask.Remarks,
            CreatedAtDate = doTask.CreatedAtDate,
            ScheduledDate = doTask.scheduledDate,
            StartDate = doTask.StartDate,
            CompleteDate = doTask.CompleteDate,
            ForeCastDate = forcaste(doTask),
            Complexity = (BO.EngineerExperience?)doTask.Complexity,
            RequiredEffortTime = doTask.RequiredEffortTime,
            Status = getstatus(doTask),
            Dependencies = (from item in depList
                            select new TaskInList()
                            {
                                Id = item.DependsOnTask,
                                Description = _dal.Task.Read(item.DependsOnTask)!.Description,
                                Alias = _dal.Task.Read(item.DependsOnTask)!.Alias,
                                Status = 0
                            }).ToList(),
            Engineer = new EngineerInTask() { Id = doTask.EngineerId, Name = eng.name }
        };
    }


    public IEnumerable<BO.TaskInList> ReadAll(Func<DO.Task, bool>? filter = null)
    {



        return (filter is not null) ?
            (from DO.Task doTask in _dal.Task.ReadAll(p => filter(p))
                 //(int?)p.Complexity <= (int?)(DO.EngineerExperience)level && p.EngineerId == 0
                 //&& EndOfTasks(p) ||)
             select new BO.TaskInList
             {
                 Id = doTask.Id,
                 Description = doTask.Description,
                 Alias = doTask.Alias,
                 Status = getstatus(doTask)
             }) :
                 (from DO.Task doTask in _dal.Task.ReadAll()

                  select new BO.TaskInList
                  {
                      Id = doTask.Id,
                      Description = doTask.Description,
                      Alias = doTask.Alias,
                      Status = getstatus(doTask)
                  });

    }

    public void Update(BO.Task item)
    {
        if (CheckData(item))
            throw new BlInvalidInputException("The data you entered is incorrect for the update task");

        DO.Task OldTask = _dal.Task.Read(item.Id) ?? throw
        new BO.BlAlreadyExistsException($"Task with ID={item.Id} does not exists");


        if (_bl.GetDate("StartDate") != DateTime.MinValue)
        {
            if (item.Engineer is not null)
            {
                if (check_id_engineer(item.Engineer.Id) is true && item.Engineer.Id != 0)
                    throw new BlInvalidInputException("The data you entered is incorrect for the task's engineer");


            }
        }

        if (item.StartDate is not null)
            if (item.StartDate < _bl.GetDate("StartDate"))
                throw new BlInvalidInputException("The start date can't be earlier the project's date start");

        DO.Task NewdoTask = new DO.Task(item.Id, item.Engineer!.Id, item.Alias, item.Deliverables, item.Description,
           item.Remarks, item.CreatedAtDate, item.ScheduledDate, item.StartDate, item.CompleteDate,
            (DO.EngineerExperience?)item.Complexity, item.RequiredEffortTime);

        _dal.Task.Update(NewdoTask);



    }

    public IEnumerable<BO.TaskInGantt> GanttList(DateTime date)
    {
        var x= from task in _dal.Task.ReadAll()
               select new BO.TaskInGantt()
               {
                   Id = task.Id,
                   Name = task.Description!,
                   StartOffset = (int)(task.scheduledDate - date).Value.TotalMinutes,
                   TaskLenght = (int)task.RequiredEffortTime!.Value.TotalMinutes,
                   Status = getstatus(task),
                   CompliteValue = CalcValue(task)
               };
        return x;
    }

    bool CheckData(BO.Task item)
    {
        return (item.Id <= 0 || item.Alias is null || item.Alias == "" || item.Engineer!.Id < 0);
    }

    public void UpdateDate(DateTime d, int id)
    {
        if (d < DateTime.Now)
            throw new BlLogicalErrorException("The date you entered has already passed");

        if (_bl.GetDate("StartDate") == DateTime.MinValue)
            throw new BlLogicalErrorException("Cant not update dates before you enter a start date for the project");

        if (_bl.GetDate("StartDate") > d)
            throw new BlLogicalErrorException("The start date cannot be earlier than the project start date");



        DO.Task? dotask = _dal.Task.Read(id);
        if (dotask is null)
            throw new BlNullPropertyException($"Task with ID={id} does Not exist");
        var Tasks = from DO.Dependency doDependency in _dal.Dependency.ReadAll(p => p.DependentTask == id)
                    let date = _dal.Task.Read(doDependency.DependsOnTask)!.scheduledDate
                    where date is null
                    select doDependency;
        if (Tasks.Count() != 0)
            throw new BlLogicalErrorException("The dates of the tasks on which the task depends are not yet updated");
        if (Tasks.FirstOrDefault(p => forcaste(_dal.Task.Read(p.DependsOnTask)!) < d) is not null)
            throw new BlLogicalErrorException("It is not possible to update a date for" +
                "a task that is earlier than the date of a task on which it depends");
        DO.Task? UpdateTask = dotask with { scheduledDate = d };



        _dal.Task.Update(UpdateTask);

    }

    public void AddDependency(int IdDepented, int IdDepentedOn)


    {

        if (IdDepented == IdDepentedOn)
            throw new BlLogicalErrorException("A task cannot depend on itself");

        if (_bl.GetDate("StartDate") != DateTime.MinValue)
            throw new BlLogicalErrorException("Dependencies cannot be added after the execution phase has started");



        BO.Task? Task_Depented = Read(IdDepented);
        BO.Task? botask = Read(IdDepentedOn);
        if (botask is null || Task_Depented is null)
            throw new BlNullPropertyException($"The task does not exist");
        if (Task_Depented.Dependencies is not null)
        {
            if (Task_Depented.Dependencies.Any(k => k.Id == IdDepentedOn) is true)
                throw new BlLogicalErrorException("The task already depends on this task");

        }
        if (Circularity_test(IdDepented, IdDepentedOn) is true)
            throw new BlNullPropertyException("There is a circular dependency in the tasks!");

        BO.TaskInList newTask = new BO.TaskInList()
        {
            Id = IdDepentedOn,
            Description = botask.Description,
            Alias = botask.Alias,
            Status = botask.Status
        };
        BO.Task? temp = Read(IdDepented);



        DO.Dependency doDependency = new DO.Dependency(0, IdDepented, IdDepentedOn);
        _dal.Dependency.Create(doDependency);
        temp!.Dependencies!.Add(newTask);
    }


    public bool Circularity_test(int task_id, int dep_id)
    {
        if (task_id == dep_id)
            return true;

        BO.Task? dep_task = Read(dep_id);

        if (dep_task is not null)
        {
            if (dep_task.Dependencies is null)
                return false;
        }
        else
            throw new BlNullPropertyException("The task does not exist");

        foreach (var task in dep_task.Dependencies)
        {
            if (Circularity_test(task_id, task.Id) is true)
                return true;

        }




        return false;

    }


    public void DeleteDependency(int IdDepented, int IdDepentedOn)
    {
        if (_bl.GetDate("StartDate") != DateTime.MinValue)
            throw new BlLogicalErrorException("Dependencies cannot be deleted after the execution phase has started");

        BO.Task? Task_Depented = Read(IdDepented);
        BO.Task? botask = Read(IdDepentedOn);
        if (botask is null || Task_Depented is null)
            throw new BlNullPropertyException($"The task does not exist");
        if (Task_Depented.Dependencies is not null)
            if (Task_Depented.Dependencies.RemoveAll(x => x?.Id == IdDepentedOn) == 0)
                throw new BlNullPropertyException($"The task does not depend on the task you entered");





        DO.Dependency? dep_for_delete = _dal.Dependency.Read(x => x.DependentTask == IdDepented
        && x.DependsOnTask == IdDepentedOn);
        if (dep_for_delete is not null)
            _dal.Dependency.Delete(dep_for_delete.Id);

    }

    void UpdateEngineer(int id, EngineerInTask engineer)
    {
        if (_bl.GetDate("StartDate") == DateTime.MinValue)
            throw new BlLogicalErrorException("It is not possible to assign an engineer before the start of the execution phase");




        DO.Task? dotask = _dal.Task.Read(id);
        DO.Engineer? doengineer = _dal.Engineer.Read(engineer.Id);
        if (engineer.Name is null || engineer.Name == "" || engineer.Id <= 0 || id <= 0 || dotask is null || doengineer is null)
            throw new BlInvalidInputException("The data entered does not match");


        if ((int?)dotask.Complexity > (int?)doengineer.level)
            throw new BlLogicalErrorException("The task level is not suitable for an engineer");
        //מאחר והמנהדס עובר למשימה חדשה צמריך לבדוק אם הייתה לו משימה קודמת ולעדכן בהתאם לכך שזמן הסיום יהיה כעת ואז הסטטוס יהיה done 

        var item = _dal.Task.ReadAll(p => p.EngineerId == engineer.Id);
        if (item.Any())
            foreach (var item2 in item)
                if (item2.CompleteDate > _bl.Clock)
                {
                    DO.Task NewTask = item2 with { CompleteDate = _bl.Clock };
                    _dal.Task.Update(NewTask);
                }







        DO.Task UpdateTask = dotask with { EngineerId = engineer.Id };
        _dal.Task.Update(UpdateTask);

    }

    DateTime? forcaste(DO.Task T)
    {

        DateTime? max = T.StartDate >= T.scheduledDate ? T.StartDate : T.scheduledDate;

        //max = max + T.RequiredEffortTime;

        return max;
    }

    public Status getstatus(DO.Task T)
    {

        int i = 0;
        if (T.StartDate is not null)
            i = 1;
        if (T.CompleteDate is not null)
            if (T.CompleteDate <= _bl.Clock)
                i = 3;
            else
                i = 2;

        switch (i)
        {
            case 0:
                return (Status)0;

            case 1:
                return (Status)1;

            case 2:
                return (Status)2;

            case 3:
                return (Status)3;

            default:
                return 0;

        }
    }
    //מתודת עזר בשביל חישוב האם כל המשימות התלוות במשימה זו הסתיימו
    bool EndOfTasks(DO.Task T)
    {
        if (_dal.Dependency.ReadAll(p => p.DependentTask == T.Id)
            .FirstOrDefault(k => getstatus(_dal.Task.Read(k.DependsOnTask)!) != BO.Status.Done) is not null)
            return false;
        return true;
    }

    private int CalcValue(DO.Task task)
    {
        if (task.StartDate is null)
            return 0;

        DateTime clock = Factory.Get().Clock;
        if (clock > task.StartDate && task.CompleteDate is null)
            return (int)((double)(clock - task.StartDate).Value.TotalDays / (double)task.RequiredEffortTime!.Value.TotalDays) * 100;

        return 0;
    }

    public void ScheduleTasks(DateTime startDate)
    {
        Dictionary<int, DO.Task> tasks = _dal.Task.ReadAll().ToList().ToDictionary(task => task.Id);
        List<Dependency> dependencies = _dal.Dependency.ReadAll().ToList();


        // Initialize the schedule with tasks that have no dependencies
        Dictionary<int, DO.Task> schedule = tasks.Where(task => !dependencies.Any(dep => dep.DependentTask == task.Key)).
            Select(task => task.Value).ToList().ToDictionary(task => task.Id);

        foreach (int key in schedule.Keys)
        {
            DO.Task old = schedule[key];
            TimeSpan? lenghTask = old.RequiredEffortTime;
            old = old with { scheduledDate = startDate, DeadLine = startDate + lenghTask };
            schedule[key] = old;
        }

        foreach (int task in tasks.Keys)
        {
            if (schedule.ContainsKey(task))
                tasks.Remove(task);
        }


        while (tasks.Count > 0)
        {
            foreach (int newTask in tasks.Keys)
            {
                bool canSchedule = true;

                foreach (Dependency dep in dependencies.Where(dep => dep.DependentTask == newTask))
                {
                    if (!schedule.ContainsKey(dep.DependsOnTask))
                    {
                        canSchedule = false;
                        break;
                    }
                }

                if (canSchedule)
                {
                    DateTime? earlyStart = DateTime.MinValue;
                    DateTime? lastDepDate = DateTime.MinValue;

                    foreach (Dependency dep in dependencies.Where(dep => dep.DependentTask == newTask))
                    {
                        lastDepDate = schedule[dep.DependsOnTask].DeadLine;
                        if (lastDepDate > earlyStart)
                            earlyStart = lastDepDate;
                    }
                    tasks[newTask] = tasks[newTask] with { scheduledDate = earlyStart, DeadLine = earlyStart + tasks[newTask].RequiredEffortTime };

                    schedule.Add(newTask, tasks[newTask]);
                    tasks.Remove(newTask);
                }
            }
        }

        schedule.Values.ToList().ForEach(task => { _dal.Task.Update(task); });
    }


}






