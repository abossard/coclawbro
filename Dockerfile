FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Copy project files first for layer caching
COPY CoClawBro.Core/CoClawBro.Core.csproj CoClawBro.Core/
COPY CoClawBro.App/CoClawBro.App.csproj CoClawBro.App/
COPY CoClawBro.slnx .
RUN dotnet restore CoClawBro.App/CoClawBro.App.csproj

# Copy source and publish AOT for the target architecture
COPY . .
RUN DOTNET_RID="linux-$([ "$TARGETARCH" = "amd64" ] && echo "x64" || echo "$TARGETARCH")" && \
    dotnet publish CoClawBro.App/CoClawBro.App.csproj \
      -c Release \
      -r "$DOTNET_RID" \
      -p:PublishAot=true \
      -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["./coclawbro"]
