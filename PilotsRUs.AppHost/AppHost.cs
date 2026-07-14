var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.PilotsRUs_API_WebApi>("api");

builder.AddProject<Projects.PilotsRUs_Admin_App>("admin")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
