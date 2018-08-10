﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.Azure.Commands.RecoveryServices.Backup.Cmdlets.Models;
using Microsoft.Azure.Commands.RecoveryServices.Backup.Helpers;
using Microsoft.Azure.Commands.RecoveryServices.Backup.Properties;
using Microsoft.Azure.Management.RecoveryServices.Backup.Models;
using RestAzureNS = Microsoft.Rest.Azure;

namespace Microsoft.Azure.Commands.RecoveryServices.Backup.Cmdlets.ServiceClientAdapterNS
{
    public partial class ServiceClientAdapter
    {
        /// <summary>
        /// Restores the disk based on the recovery point and other input parameters
        /// </summary>
        /// <param name="rp">Recovery point to restore the disk to</param>
        /// <param name="storageAccountId">ID of the storage account where to restore the disk</param>
        /// <param name="storageAccountLocation">Location of the storage account where to restore the disk</param>
        /// <param name="storageAccountType">Type of the storage account where to restore the disk</param>
        /// <returns>Job created by this operation</returns>
        public RestAzureNS.AzureOperationResponse RestoreDisk(
            AzureVmRecoveryPoint rp,
            string storageAccountId,
            string storageAccountLocation,
            string storageAccountType,
            string targetResourceGroupName,
            bool osaOption,
            string vaultName = null,
            string resourceGroupName = null,
            string vaultLocation = null)
        {
            var useOsa = ShouldUseOsa(rp, osaOption);

            vaultLocation = vaultLocation ?? BmsAdapter.GetResourceLocation();
            Dictionary<UriEnums, string> uriDict = HelperUtils.ParseUri(rp.Id);
            string containerUri = HelperUtils.GetContainerUri(uriDict, rp.Id);
            string protectedItemUri = HelperUtils.GetProtectedItemUri(uriDict, rp.Id);
            string recoveryPointId = rp.RecoveryPointId;
            //validtion block
            if (storageAccountLocation != vaultLocation)
            {
                throw new Exception(Resources.RestoreDiskIncorrectRegion);
            }

            string vmType = containerUri.Split(';')[1].Equals("iaasvmcontainer", StringComparison.OrdinalIgnoreCase)
                ? "Classic" : "Compute";
            string strType = storageAccountType.Equals("Microsoft.ClassicStorage/StorageAccounts",
                StringComparison.OrdinalIgnoreCase) ? "Classic" : "Compute";
            if (vmType != strType)
            {
                throw new Exception(string.Format(Resources.RestoreDiskStorageTypeError, vmType));
            }

            if (targetResourceGroupName != null && rp.IsManagedVirtualMachine == false)
            {
                Logger.Instance.WriteWarning(Resources.UnManagedBackupVmWarning);
            }

            IaasVMRestoreRequest restoreRequest = new IaasVMRestoreRequest()
            {
                CreateNewCloudService = false,
                RecoveryPointId = recoveryPointId,
                RecoveryType = RecoveryType.RestoreDisks,
                Region = vaultLocation,
                StorageAccountId = storageAccountId,
                SourceResourceId = rp.SourceResourceId,
                TargetResourceGroupId = targetResourceGroupName != null ?
                    "/subscriptions/" + BmsAdapter.Client.SubscriptionId + "/resourceGroups/" + targetResourceGroupName :
                    null,
                OriginalStorageAccountOption = useOsa,
            };

            RestoreRequestResource triggerRestoreRequest = new RestoreRequestResource();
            triggerRestoreRequest.Properties = restoreRequest;

            var response = BmsAdapter.Client.Restores.TriggerWithHttpMessagesAsync(
                vaultName ?? BmsAdapter.GetResourceName(),
                resourceGroupName ?? BmsAdapter.GetResourceGroupName(),
                AzureFabricName,
                containerUri,
                protectedItemUri,
                recoveryPointId,
                triggerRestoreRequest,
                cancellationToken: BmsAdapter.CmdletCancellationToken).Result;

            return response;
        }

        private bool ShouldUseOsa(AzureVmRecoveryPoint rp, bool osaOption)
        {
            bool useOsa = false;
            if (osaOption)
            {
                if (rp.OriginalSAEnabled)
                {
                    useOsa = true;
                }
                else
                {
                    throw new Exception("This recovery point doesn’t have the capability to restore disks to their original storage account. Re-run the restore command without the UseOriginalStorageAccountForDisks parameter.");
                }
            }

            return useOsa;
        }
    }
}