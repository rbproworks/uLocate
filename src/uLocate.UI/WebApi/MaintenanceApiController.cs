﻿namespace uLocate.UI.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.Http;
    using Indexer;
    using uLocate.Helpers;
    using uLocate.Models;
    using uLocate.Services;
    using uLocate.WebApi;
    using Umbraco.Core.Logging;
    using Umbraco.Web.WebApi;

    /// <summary>
    /// The location type api controller for use by the umbraco back-office
    /// </summary>
    [Umbraco.Web.Mvc.PluginController("uLocate")]
    public class MaintenanceApiController : UmbracoAuthorizedApiController
    {
        private LocationTypeService locTypeService = new LocationTypeService();
        private LocationService locService = new LocationService();
        //private uLocate.Indexer.LocationIndexManager locationIndexManager = new LocationIndexManager();

        /// /umbraco/backoffice/uLocate/MaintenanceApi/Test
        [System.Web.Http.AcceptVerbs("GET")]
        public bool Test()
        {
            return true;
        }

        #region Querying
        /// <summary>
        /// Gets a list of all countries and their codes.
        /// /umbraco/backoffice/uLocate/MaintenanceApi/GetCountryCodes
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerable"/> of <see cref="Country"/>.
        /// </returns>
        [System.Web.Http.AcceptVerbs("GET")]
        public IEnumerable<Country> GetCountryCodes()
        {
            return CountryHelper.GetAllCountries();
        }

        /// <summary>
        /// Gets information about which Locations need their geography updated
        /// /umbraco/backoffice/uLocate/MaintenanceApi/GeographyNeedsUpdated
        /// </summary>
        /// <returns>
        /// The <see cref="MaintenanceCollection"/>.
        /// </returns>
        [System.Web.Http.AcceptVerbs("GET")]
        public MaintenanceCollection GeographyNeedsUpdated()
        {
            //Repositories.LocationRepo.SetMaintenanceFlags();

            var maintColl = new MaintenanceCollection();
            maintColl.Title = "Database 'Geography' Data Needs Updated";

            maintColl.IndexedLocations = locService.GetAllMissingDbGeo();
            //maintColl.ConvertToJsonLocationsOnly();

            return maintColl;
        }

        /// <summary>
        /// Count locations by region
        /// /umbraco/backoffice/uLocate/MaintenanceApi/CountLocationsByRegion?LocTypeKey=xxx
        /// </summary>
        /// <param name="LocationTypeKey">
        /// The location type key.
        /// </param>
        /// <returns>
        /// A <see cref="StatusMessage"/>.
        /// </returns>
        [System.Web.Http.AcceptVerbs("GET")]
        public StatusMessage CountLocationsByRegion(Guid LocTypeKey)
        {
            return locService.CountLocations(LocTypeKey, n => n.Region);
        }

        /// <summary>
        /// Count locations by Location Type
        /// /umbraco/backoffice/uLocate/MaintenanceApi/CountLocationsByType
        /// </summary>
        /// <param name="LocationTypeKey">
        /// The location type key.
        /// </param>
        /// <returns>
        /// A <see cref="StatusMessage"/>.
        /// </returns>
        [System.Web.Http.AcceptVerbs("GET")]
        public StatusMessage CountLocationsByType()
        {
            return locService.CountLocationsByType();
        }

        /// <summary>
        /// Gets information about which Locations have missing Lat/Long values (thus might be invalid addresses)
        /// /umbraco/backoffice/uLocate/MaintenanceApi/GetLocsWithMissingCoordinates
        /// </summary>
        /// <returns>
        /// A <see cref="MaintenanceCollection"/>.
        /// </returns>
        [System.Web.Http.AcceptVerbs("GET")]
        public MaintenanceCollection GetLocsWithMissingCoordinates()
        {
            //Repositories.LocationRepo.SetMaintenanceFlags();

            var maintColl = new MaintenanceCollection();
            maintColl.Title = "Addresses missing coordinates";

            var locMissing = new List<IndexedLocation>();
            var locMissingLat = locService.GetLocationsByPropertyValue("Latitude", 0);
            var locMissingLong = locService.GetLocationsByPropertyValue("Longitude", 0);

            locMissing.AddRange(locMissingLat);
            locMissing.AddRange(locMissingLong);

            maintColl.IndexedLocations = locMissing;
            maintColl.SyncLocationLists();

            return maintColl;
        }


        #endregion

        #region Updates

        /// <summary>
        /// Updates Lat/Long coordinates for all Locations which require it.
        /// /umbraco/backoffice/uLocate/MaintenanceApi/UpdateCoordinatesAsNeeded
        /// </summary>
        /// <returns>
        /// The <see cref="StatusMessage"/>.
        /// </returns>
        [System.Web.Http.AcceptVerbs("GET")]
        public StatusMessage UpdateCoordinatesAsNeeded()
        {
            var Result = locService.UpdateCoordinatesAsNeeded();

            return Result;
        }
        
        #endregion

        #region Deletes

        /// <summary>
        /// Delete all locations with a matching name
        /// /umbraco/backoffice/uLocate/MaintenanceApi/DeleteLocationsByName?LocName=xxx
        /// </summary>
        /// <param name="LocName">
        /// The loc name.
        /// </param>
        /// <returns>
        /// The <see cref="StatusMessage"/>.
        /// </returns>
        [System.Web.Http.AcceptVerbs("GET")]
        public StatusMessage DeleteLocationsByName(string LocName)
        {
            var Msg = new StatusMessage();
            Msg.ObjectName = LocName;
            
            var matchingLocations = locService.GetLocations(LocName);
            if (matchingLocations.Any())
            {
                foreach (var loc in matchingLocations)
                {
                   locService.DeleteLocation(loc.Key);
                }

                Msg.Message = string.Format("{0} location(s) named '{1}' were found and deleted.", matchingLocations.Count(), LocName);
            }
            else
            {
                Msg.Message = string.Format("No locations named '{0}' were found.", LocName);
            }
            Msg.Success = true;
            return Msg;
        }

        /// <summary>
        /// Delete all locations of the specified type
        /// /umbraco/backoffice/uLocate/MaintenanceApi/DeleteAllLocationsOfType?LocationTypeKey=xxx
        /// </summary>
        /// <param name="LocationTypeKey">
        /// The Location Type Key.
        /// </param>
        /// <returns>
        /// The <see cref="StatusMessage"/>.
        /// </returns>
        [System.Web.Http.AcceptVerbs("GET")]
        public StatusMessage DeleteAllLocationsOfType(Guid LocationTypeKey)
        {
            var Msg = new StatusMessage();
            var locTypeName = locTypeService.GetLocationType(LocationTypeKey).Name;
            Msg.ObjectName = locTypeName;

            var matchingLocations = locService.GetLocations(LocationTypeKey);
            if (matchingLocations.Any())
            {
                foreach (var loc in matchingLocations)
                {
                  locService.DeleteLocation(loc.Key);
                }

                Msg.Message = string.Format("{0} location(s) of type '{1}' were found and deleted.", matchingLocations.Count(), locTypeName);
            }
            else
            {
                Msg.Message = string.Format("No locations of type '{0}' were found.", locTypeName);
            }

            Msg.Success = true;
            return Msg;
        }


        /// <summary>
        /// Delete all locations.
        /// /umbraco/backoffice/uLocate/MaintenanceApi/DeleteAllLocations?Confirm=true
        /// </summary>
        /// <param name="Confirm">
        /// The confirmation (must be TRUE to run)
        /// </param>
        /// <returns>
        /// The <see cref="StatusMessage"/>.
        /// </returns>
        [System.Web.Http.AcceptVerbs("GET")]
        public StatusMessage DeleteAllLocations(bool Confirm)
        {
            var ResultMsg = new StatusMessage();

            if (Confirm)
            {
                int delCounter = 0;
                List<Guid> allKeys = new List<Guid>();

                var allLocations = locService.GetLocations();
                var totCounter = allLocations.Count();

                foreach (var loc in allLocations)
                {
                    allKeys.Add(loc.Key);
                }

                foreach (var key in allKeys)
                {
                    var stat = locService.DeleteLocation(key);
                    ResultMsg.InnerStatuses.Add(stat);
                    if (stat.Success)
                    {
                        delCounter++;
                    }
                }

                ResultMsg.Message = string.Format("{0} Location(s) deleted out of a total of {1} Location(s)", delCounter, totCounter);
                ResultMsg.Success = true;
            }
            else
            {
                ResultMsg.Success = false;
                ResultMsg.Code = "NotConfirmed";
                ResultMsg.Message = "The operation was not confirmed, and thus did not run.";
            }

            return ResultMsg;
        }

        #endregion

        #region Indexing
        ///// /umbraco/backoffice/uLocate/TestApi/RemoveFromIndex?LocationKey=xxx
        //[AcceptVerbs("GET")]
        //public StatusMessage RemoveFromIndex(Guid LocationKey)
        //{
        //    LogHelper.Info<PublicApiController>("RemoveFromIndex STARTED/ENDED");
        //    var Location = locService.GetLocation(LocationKey).ConvertToEditableLocation();
        //    return this.locationIndexManager.RemoveLocation(Location);
        //}

        /// /umbraco/backoffice/uLocate/TestApi/ReIndexAll
        [AcceptVerbs("GET")]
        public StatusMessage ReIndexAll()
        {
            LogHelper.Info<PublicApiController>("ReIndex STARTED");
            var result = locService.ReindexAllLocations();
            LogHelper.Info<PublicApiController>("ReIndex ENDED");

            return result;
        }

        #endregion

    }
}

