#	Use a Microsoft image with .NET core runtime (https://mcr.microsoft.com/en-us/product/dotnet/aspnet/about)
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS final

#	Set the working directory
WORKDIR /work

#	Copy package
COPY . .

#	Define environment variables
ENV TODO ""

#	Run console app
CMD ["dotnet", "KustoPreForgeConsole.dll"]