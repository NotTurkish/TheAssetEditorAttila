﻿using System.Collections.Generic;

namespace CommonControls.Interfaces.AssetManagement
{
    public interface IAssetManagementFactory
    {
        List<IAssetImporter> GetAllImporters();
        T GetImporter<T>() where T : IAssetImporter;
        IAssetImporter GetImporter(string format);
    }
}
