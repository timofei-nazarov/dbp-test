FROM mcr.microsoft.com/dotnet/sdk:10.0 AS sdk
WORKDIR /workspace
COPY backend/TaskManager.Api/TaskManager.Api.csproj backend/TaskManager.Api/
COPY backend/TaskManager.Api/packages.lock.json backend/TaskManager.Api/
RUN dotnet restore backend/TaskManager.Api/TaskManager.Api.csproj --locked-mode
COPY backend backend
COPY TaskManager.slnx .
COPY .config .config
RUN dotnet restore backend/TaskManager.Tests/TaskManager.Tests.csproj --locked-mode

FROM sdk AS publish
RUN dotnet publish backend/TaskManager.Api/TaskManager.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /out .
RUN mkdir /app/media && chown -R app:app /app/media
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskManager.Api.dll"]
