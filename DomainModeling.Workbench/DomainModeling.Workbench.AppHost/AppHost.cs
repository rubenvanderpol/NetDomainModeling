var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.DomainModeling_Workbench_Api>("api")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var web = builder.AddViteApp("web", "../DomainModeling.Workbench.Web")
    .WithReference(api)
    .WaitFor(api);

api.PublishWithContainerFiles(web, "wwwroot");

builder.Build().Run();
