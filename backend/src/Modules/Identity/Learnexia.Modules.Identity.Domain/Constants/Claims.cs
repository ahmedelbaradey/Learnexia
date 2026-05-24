namespace Learnexia.Modules.Identity.Domain.Constants;

public static partial class Claims
{
    public static List<string> GenerateModules() => new()
    {
        "Catalog",
        "Learning",
        "Parent",
    };


    

    
    public static List<string> GeneratePermissions()
    {
        var permissions = new List<string>();
        foreach (var module in GenerateModules())
        {
            var modulepermissions = new List<string>
        {
            $"{module}.View",
            $"{module}.List",
            $"{module}.Create",
            $"{module}.Edit",
            $"{module}.Delete",
        };
        permissions.AddRange(modulepermissions);
        }
        

        return permissions;
    }
}
