using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using BeaverSoft.Texo.Commands.NugetManager.Model;
using BeaverSoft.Texo.Commands.NugetManager.Processing;

namespace BeaverSoft.Texo.Commands.NugetManager.Services
{
    public class PackageManagementService : IPackageManagementService
    {
        private readonly ISourceManagementService sources;
        private ImmutableSortedDictionary<string, IPackageInfo> packages;

        public PackageManagementService(ISourceManagementService sources)
        {
            this.sources = sources ?? throw new ArgumentNullException(nameof(sources));
            ResetPackages();
        }

        public IEnumerable<IPackageInfo> GetAll()
        {
            return packages.Values;
        }

        IPackageInfo IPackageSource.GetPackage(string packageId)
        {
            return GetOrFetch(packageId);
        }

        public IPackageInfo GetOrFetch(string packageId)
        {
            if (packages.TryGetValue(packageId, out IPackageInfo package))
            {
                return package;
            }

            return Fetch(packageId);
        }

        public bool TryGet(string packageId, out IPackageInfo package)
        {
            return packages.TryGetValue(packageId, out package);
        }

        public IPackageInfo Fetch(string packageId)
        {
            IPackageInfo package = sources.FetchPackage(packageId);

            if (package == null)
            {
                return null;
            }

            packages = packages.SetItem(package.Id, package);
            return package;
        }

        public IEnumerable<IPackageInfo> Search(string searchTerm)
        {
            return sources.SearchPackages(searchTerm).Values;
        }

        public void Clear()
        {
            ResetPackages();
        }

        private void ResetPackages()
        {
            packages = ImmutableSortedDictionary.Create<string, IPackageInfo>(new InsensitiveStringComparer());
        }

        
    }
}
