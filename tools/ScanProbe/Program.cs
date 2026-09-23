using VeFoldery.Core.Services;

var folder = args[0];
var svc = new FileOrganizerService("VeFoldery", folder, folder);

var a = svc.ScanFiles(true, ignoreSystemFiles: true);
Console.WriteLine($"ignoreSystemFiles=true : {a.Count} items");
var b = svc.ScanFiles(true, ignoreSystemFiles: false);
Console.WriteLine($"ignoreSystemFiles=false: {b.Count} items");
