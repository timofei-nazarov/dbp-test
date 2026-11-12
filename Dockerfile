FROM mcr.microsoft.com/dotnet/sdk:10.0 AS sdk
WORKDIR /workspace
COPY global.json Directory.Build.props TaskManager.slnx ./
COPY .config .config
COPY src/TaskManager.Contracts/TaskManager.Contracts.csproj src/TaskManager.Contracts/
COPY src/TaskManager.Contracts/packages.lock.json src/TaskManager.Contracts/
COPY src/TaskManager.Domain/TaskManager.Domain.csproj src/TaskManager.Domain/
COPY src/TaskManager.Domain/packages.lock.json src/TaskManager.Domain/
COPY src/TaskManager.Infrastructure/TaskManager.Infrastructure.csproj src/TaskManager.Infrastructure/
COPY src/TaskManager.Infrastructure/packages.lock.json src/TaskManager.Infrastructure/
COPY src/TaskManager.Web/TaskManager.Web.csproj src/TaskManager.Web/
COPY src/TaskManager.Web/packages.lock.json src/TaskManager.Web/
COPY src/TaskManager.Api/TaskManager.Api.csproj src/TaskManager.Api/
COPY src/TaskManager.Api/packages.lock.json src/TaskManager.Api/
COPY tests/TaskManager.Api.Tests/TaskManager.Api.Tests.csproj tests/TaskManager.Api.Tests/
COPY tests/TaskManager.Api.Tests/packages.lock.json tests/TaskManager.Api.Tests/
COPY tests/TaskManager.Web.Tests/TaskManager.Web.Tests.csproj tests/TaskManager.Web.Tests/
COPY tests/TaskManager.Web.Tests/packages.lock.json tests/TaskManager.Web.Tests/
RUN dotnet restore TaskManager.slnx --locked-mode
COPY src src
COPY tests tests

FROM sdk AS publish
RUN dotnet publish src/TaskManager.Api/TaskManager.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /out .
RUN mkdir /app/media && chown -R app:app /app/media
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskManager.Api.dll"]
