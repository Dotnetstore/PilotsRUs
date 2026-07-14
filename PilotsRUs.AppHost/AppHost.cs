var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var db = postgres.AddDatabase("pilotsrus");

var api = builder.AddProject<Projects.PilotsRUs_API_WebApi>("api")
    .WithReference(db)
    .WaitFor(db);

builder.AddProject<Projects.PilotsRUs_Admin_App>("admin")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
