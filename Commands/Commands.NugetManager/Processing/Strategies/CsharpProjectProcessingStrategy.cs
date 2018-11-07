﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Xml.Linq;
using BeaverSoft.Texo.Commands.NugetManager.Model.Packages;
using BeaverSoft.Texo.Commands.NugetManager.Model.Projects;
using StrongBeaver.Core.Services.Logging;

namespace BeaverSoft.Texo.Commands.NugetManager.Processing.Strategies
{
    public class CsharpProjectProcessingStrategy : IProjectProcessingStrategy
    {
        private const string PACKAGES_CONFIG_FILE_NAME = "packages.config";

        private readonly ILogService logger;

        public CsharpProjectProcessingStrategy(ILogService logger)
        {
            this.logger = logger;
        }

        public IProject Process(string filePath)
        {
            IXmlContentLoader loader = new XmlContentLoader(logger);
            loader.Load(filePath);

            if (!loader.IsSuccess)
            {
                return null;
            }

            ProjectInfo info = ProcessProject(loader.Content, loader.FilePath);

            if (info == null)
            {
                return null;
            }

            if (!info.IsNewFormat)
            {
                ProcessPackageConfig(info);
            }

            return new Project(info.Path, info.Packages.ToImmutableList());
        }

        private ProjectInfo ProcessProject(XDocument document, Uri filePath)
        {
            XElement root = document.Root;

            if (root == null || root.Name.LocalName != "Project")
            {
                return null;
            }

            bool isNewFormat = root.Attribute("Sdk") != null;
            XNamespace xmlNamespace = root.GetDefaultNamespace();
            List<IPackage> packages = new List<IPackage>();

            foreach (XElement elementReference in root.Descendants(xmlNamespace + "PackageReference"))
            {
                string packageId = (string)elementReference.Attribute("Include");
                string version = (string)elementReference.Attribute("Version");

                if (string.IsNullOrWhiteSpace(packageId)
                    || string.IsNullOrWhiteSpace(version))
                {
                    logger.Warn("Invalid PackageReference element in csproj.", filePath, elementReference);
                    continue;
                }

                packages.Add(new Package(packageId, version));
            }

            return new ProjectInfo()
            {
                Path = filePath,
                Packages = packages,
                IsNewFormat = isNewFormat || packages.Count > 0,
            };
        }

        private void ProcessPackageConfig(ProjectInfo info)
        {
            string directoryPath = Path.GetDirectoryName(info.Path.AbsolutePath);
            string configPath = Path.Combine(directoryPath, PACKAGES_CONFIG_FILE_NAME);

            IXmlContentLoader loader = new XmlContentLoader(logger);
            loader.Load(configPath);

            if (!loader.IsSuccess)
            {
                return;
            }

            XElement root = loader.Content.Root;

            if (root == null || root.Name.LocalName != "packages")
            {
                return;
            }

            XNamespace xmlNamespace = root.GetDefaultNamespace();

            foreach (XElement elementPackage in root.Elements(xmlNamespace + "package"))
            {
                string packageId = (string)elementPackage.Attribute("id");
                string version = (string)elementPackage.Attribute("version");

                if (string.IsNullOrWhiteSpace(packageId)
                    || string.IsNullOrWhiteSpace(version))
                {
                    logger.Warn("Invalid package definition in " + PACKAGES_CONFIG_FILE_NAME + ".", loader.FilePath, elementPackage);
                    continue;
                }

                info.Packages.Add(new Package(packageId, version));
            }
        }

        private class ProjectInfo
        {
            public Uri Path { get; set; }

            public bool IsNewFormat { get; set; }

            public List<IPackage> Packages { get; set; }
        }
    }
}
