﻿namespace uLocate.Persistance
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.Mvc.Html;
    using System.Web.UI;

    using Mindfly;

    using uLocate.Data;
    using uLocate.Helpers;
    using uLocate.Models;

    using Umbraco.Core;
    using Umbraco.Core.Cache;
    using Umbraco.Core.Logging;
    using Umbraco.Core.Persistence;

    using umbraco.MacroEngines;

    /// <summary>
    /// Represents the <see cref="LocationRepository"/>.
    /// </summary>
    internal class LocationRepository : PagedRepositoryBase<EditableLocation> //, ILocationRespository
    {
        private List<EditableLocation> CurrentCollection = new List<EditableLocation>();

        /// <summary>
        /// Initializes a new instance of the <see cref="LocationRepository"/> class.
        /// </summary>
        /// <param name="database">
        /// The database.
        /// </param>
        /// <param name="cache">
        /// The cache.
        /// </param>
        public LocationRepository(UmbracoDatabase database, IRuntimeCacheProvider cache)
            : base(database, cache)
        {
        }

        #region Public & Internal Methods

        #region Operations

        internal void Insert(EditableLocation Entity)
        {
            PersistNewItem(Entity);
        }

        //public void Insert(EditableLocation Entity, out Guid NewItemKey)
        //{
        //    //TODO: might not be needed
        //    PersistNewItem(Entity);
        //    NewItemKey = Entity.Key;
        //}

        internal StatusMessage Delete(Guid LocationKey)
        {
            StatusMessage ReturnMsg = new StatusMessage();

            EditableLocation ThisLoc = this.GetByKey(LocationKey);

            if (ThisLoc != null)
            {
                this.DeleteLocation(ThisLoc, out ReturnMsg);
            }
            else
            {
                ReturnMsg.Success = false;
                ReturnMsg.Code = "NotFound";
                ReturnMsg.ObjectName = LocationKey.ToString();
                ReturnMsg.Message = string.Format("Location with key '{0}' was not found and can not be deleted.", ReturnMsg.ObjectName);
            }

            return ReturnMsg;
        }

        internal StatusMessage Delete(EditableLocation Entity)
        {
            StatusMessage ReturnMsg = new StatusMessage();
            this.DeleteLocation(Entity, out ReturnMsg);

            return ReturnMsg;
        }

        private void DeleteLocation(EditableLocation Entity, out StatusMessage StatusMsg)
        {
            StatusMessage ReturnMsg = new StatusMessage();
            ReturnMsg.ObjectName = Entity.Name;

            this.DeleteChildren(Entity);

            PersistDeletedItem(Entity, out ReturnMsg);
            StatusMsg = ReturnMsg;
        }

        internal void Update(EditableLocation Entity)
        {
            PersistUpdatedItem(Entity);
        }

        //internal void SetMaintenanceFlags()
        //{
        //    var allLocations = this.GetAll();
        //    foreach (var location in allLocations)
        //    {
        //        SetMaintenanceFlags(location);
        //    }
        //}

        //internal void SetMaintenanceFlags(EditableLocation entity)
        //{
        //    var GeogIsValid = this.GeogIsValid(entity);

        //    entity.DbGeogNeedsUpdated = !GeogIsValid;

        //    this.Update(entity);
        //}

        internal StatusMessage UpdateGeoForAllNeeded()
        {
            var ReturnMsg = new StatusMessage();

            var updateCounter = 0;
            var checkedCounter = 0;
            var dbCounter = 0;

            foreach (var location in this.GetAll())
            {
                if (location.Latitude == 0 | location.Longitude == 0)
                {
                    this.UpdateLatLong(location);
                    updateCounter++;
                }

                //Repositories.LocationRepo.SetMaintenanceFlags();

                if (!this.GeogIsValid(location))
                {
                    this.UpdateDbGeography(location);
                    dbCounter++;
                }

                checkedCounter++;
            }

            ReturnMsg.Success = true;
            ReturnMsg.Message =
                string.Format(
                    "{0} location(s) were checked for needing an update. {1} had their coordinates updated. Additionally, {2} had their database geography updated.",
                    checkedCounter,
                    updateCounter,
                    dbCounter);

            return ReturnMsg;

        }

        internal void UpdateLatLong(EditableLocation Loc)
        {
            LogHelper.Info<LocationRepository>(string.Format("UpdateLatLong for {0} STARTED", Loc.Name)); 
            var coord = DoGeocoding.GetCoordinateForAddress(Loc.Address);
            if (coord != null)
            {
                Loc.Coordinate = coord;
                Loc.Latitude = coord.Latitude;
                Loc.Longitude = coord.Longitude;
                Loc.GeocodeStatus = GeocodeStatus.Ok;
                this.Update(Loc);
                this.UpdateDbGeography(Loc);
            }
            LogHelper.Info<LocationRepository>(string.Format("UpdateLatLong for {0} COMPLETED", Loc.Name)); 
        }

        internal void UpdateDbGeography(EditableLocation Loc)
        {
            LogHelper.Info<LocationRepository>(string.Format("UpdateDbGeography for {0} STARTED", Loc.Name)); 

            if (Loc.Latitude == 0 || Loc.Longitude == 0)
            {
                LogHelper.Info<LocationRepository>(string.Format("Missing Lat or Long...")); 
                this.UpdateLatLong(Loc);
            }
            else
            {
                var sql = new Sql();
                sql.Append("UPDATE [uLocate_Location]");
                sql.Append("SET [GeogCoordinate] = geography::Point([Latitude], [Longitude], 4326)");
                sql.Append(string.Format("WHERE  ([Key] = '{0}')", Loc.Key));
                Repositories.ThisDb.Execute(sql);
                LogHelper.Info<LocationRepository>(string.Format("UpdateDbGeography for {0} COMPLETED", Loc.Name)); 
            }
        }

        internal void UpdateWithNewProps(Guid LocationTypeKey)
        {
            var allLocations = this.GetByType(LocationTypeKey);

            foreach (var location in allLocations)
            {
                location.SyncPropertiesWithType();
                this.Update(location);
            }
        }

        //Moved to 'uLocate.Helpers.Convert.LocationsToJsonLocations'
        //public IEnumerable<IndexedLocation> ConvertToJsonLocations(IEnumerable<EditableLocation> Locations)
        //{
        //    var ReturnList = new List<IndexedLocation>();

        //    foreach (var loc in Locations)
        //    {
        //        ReturnList.Add(new IndexedLocation(loc));
        //    }

        //    return ReturnList;
        //}

        #endregion

        #region Querying

        internal EditableLocation GetByKey(Guid Key)
        {
            CurrentCollection.Clear();
            var found = (EditableLocation)Get(Key);

            if (found!= null)
            {
                CurrentCollection.Add(found);
                FillChildren();
                return CurrentCollection[0];
            }
            else
            {
                return null;
            }
        }

        internal IEnumerable<EditableLocation> GetByKey(Guid[] Keys)
        {
            CurrentCollection.Clear();
            CurrentCollection.AddRange(GetAll(Keys));
            FillChildren();

            return CurrentCollection;
        }

        internal IEnumerable<EditableLocation> GetByName(string LocationName)
        {
            CurrentCollection.Clear();
            var sql = new Sql();
            sql.Select("*")
                .From<LocationDto>()
                .Where<LocationDto>(n => n.Name == LocationName);

            var dtoResult = Repositories.ThisDb.Fetch<LocationDto>(sql).ToList();

            var converter = new DtoConverter();
            CurrentCollection.AddRange(converter.ToLocationEntity(dtoResult));

            FillChildren();

            return CurrentCollection;
        }

        internal IEnumerable<EditableLocation> GetByType(Guid LocationTypeKey)
        {
            CurrentCollection.Clear();
            var sql = new Sql();
            sql.Select("*")
                .From<LocationDto>()
                .Where<LocationDto>(n => n.LocationTypeKey == LocationTypeKey);

            var dtoResult = Repositories.ThisDb.Fetch<LocationDto>(sql).ToList();

            var converter = new DtoConverter();
            CurrentCollection.AddRange(converter.ToLocationEntity(dtoResult));

            FillChildren();

            return CurrentCollection;
        }

        internal IEnumerable<EditableLocation> GetByCountry(string CountryCode)
        {
            return GetByCountry(CountryCode, Guid.Empty);
        }

        internal IEnumerable<EditableLocation> GetByCountry(string CountryCode, Guid FilterByLocationTypeKey)
        {
            CurrentCollection.Clear();

            IEnumerable<EditableLocation> allLocs;
            if (FilterByLocationTypeKey != Guid.Empty)
            {
                allLocs = this.GetByType(FilterByLocationTypeKey);
            }
            else
            {
                allLocs = this.GetAll();
            }

            var filteredLocs = allLocs.Where(l => l.Address.CountryCode == CountryCode);

            CurrentCollection = filteredLocs.ToList();

            //If this runs too slowly, perhaps change to an SQL query...
            //var sql = new Sql();
            //sql.Select("*")
            //    .From<LocationDto>()
            //    .Where<LocationDto>(l => l)

                //Example SQL
                //SELECT uLocate_Location.*
                //FROM     uLocate_Location INNER JOIN
                //                  uLocate_LocationPropertyData ON uLocate_Location.[Key] = uLocate_LocationPropertyData.LocationKey INNER JOIN
                //                  uLocate_LocationTypeProperty ON uLocate_LocationPropertyData.LocationTypePropertyKey = uLocate_LocationTypeProperty.[Key]
                //WHERE  (uLocate_Location.LocationTypeKey = 'E8AD5500-CB2B-4713-A616-4758D9F4AE72') AND (uLocate_LocationTypeProperty.Alias = N'CountryCode') AND (uLocate_LocationPropertyData.dataNvarchar = N'GB')

            //var dtoResult = Repositories.ThisDb.Query<LocationDto>(sql).ToList();

            //var converter = new DtoConverter();
            //CurrentCollection.AddRange(converter.ToLocationEntity(dtoResult));

            FillChildren();

            return CurrentCollection;
        }

        internal IEnumerable<EditableLocation> GetByPostalCode(string PostalCode)
        {
            return GetByPostalCode(PostalCode, Guid.Empty);
        }

        internal IEnumerable<EditableLocation> GetByPostalCode(string PostalCode, Guid FilterByLocationTypeKey)
        {
            CurrentCollection.Clear();

            IEnumerable<EditableLocation> allLocs;
            if (FilterByLocationTypeKey != Guid.Empty)
            {
                allLocs = this.GetByType(FilterByLocationTypeKey);
            }
            else
            {
                allLocs = this.GetAll();
            }

            var filteredLocs = allLocs.Where(l => l.Address.PostalCode == PostalCode);

            CurrentCollection = filteredLocs.ToList();

            FillChildren();

            return CurrentCollection;
        }

        internal IEnumerable<EditableLocation> GetByGeoSearch(double SearchLat, double SearchLong, int MilesDistance)
        {
            return GetByGeoSearch(SearchLat, SearchLong, MilesDistance, Guid.Empty);
        }

        internal IEnumerable<EditableLocation> GetByGeoSearch(double SearchLat, double SearchLong, int MilesDistance, Guid FilterByLocationTypeKey)
        {
            CurrentCollection.Clear();
            var sql = new Sql();
            sql.Select("*")
                .From<LocationDto>()
                .Append(GeographyHelper.GetGeoSearchSql(SearchLat, SearchLong, MilesDistance, FilterByLocationTypeKey));

            var dtoResult = Repositories.ThisDb.Query<LocationDto>(sql).ToList();

            var converter = new DtoConverter();
            CurrentCollection.AddRange(converter.ToLocationEntity(dtoResult));

            FillChildren();

            return CurrentCollection;
        }

        internal IEnumerable<EditableLocation> GetNearestLocations(double SearchLat, double SearchLong, int QuantityReturned)
        {
            return GetNearestLocations(SearchLat, SearchLong, QuantityReturned, Guid.Empty);
        }

        internal IEnumerable<EditableLocation> GetNearestLocations(double SearchLat, double SearchLong, int QuantityReturned, Guid FilterByLocationTypeKey)
        {
            CurrentCollection.Clear();
            var sql = new Sql();
            sql.Select(string.Format("TOP({0}) *", QuantityReturned))
                .From<LocationDto>()
                .Append(GeographyHelper.GetGeoNearestSql(SearchLat, SearchLong, FilterByLocationTypeKey));
          
            var dtoResult = Repositories.ThisDb.Query<LocationDto>(sql).ToList();

            var converter = new DtoConverter();
            CurrentCollection.AddRange(converter.ToLocationEntity(dtoResult));

            FillChildren();

            return CurrentCollection;
        }

        internal IEnumerable<EditableLocation> GetByCustomQuery(Sql SqlQuery)
        {
            CurrentCollection.Clear();

            var dtoResult = Repositories.ThisDb.Query<LocationDto>(SqlQuery).ToList();

            var converter = new DtoConverter();
            CurrentCollection.AddRange(converter.ToLocationEntity(dtoResult));

            FillChildren();

            return CurrentCollection;
        }

        internal IEnumerable<EditableLocation> GetAll()
        {
            var EmptyParams = new Guid[] { };

            CurrentCollection.Clear();
            CurrentCollection.AddRange(GetAll(EmptyParams));
            FillChildren();

            return CurrentCollection;
        }

        internal IEnumerable<EditableLocation> GetAllMissingDbGeo()
        {
            var allLocations = this.GetAll();
            CurrentCollection.Clear();

            foreach (var location in allLocations)
            {
                var GeogIsValid = this.GeogIsValid(location);

                if (!GeogIsValid)
                {
                    CurrentCollection.Add(location);
                }
            }

            return CurrentCollection;
        }

        internal string GetCount()
        {
            var sql = new Sql();
            sql.Append(
                "SELECT SUM (row_count) FROM sys.dm_db_partition_stats WHERE object_id=OBJECT_ID('uLocate_Location') AND (index_id=0 or index_id=1);");

            var result = Repositories.ThisDb.Fetch<string>(sql);

            return result[0];
        }

        //internal string GetCountByZip()
        //{

        //    var getPostalCodeKeySql = new Sql();
        //    getPostalCodeKeySql.Append("SELECT [Key] FROM uLocate_LocationTypeProperty WHERE Alias = 'PostalCode'");
        //    var result = Repositories.ThisDb.Fetch<PostalCodeAlias>(getPostalCodeKeySql);
        //    var key = result[0].Key;


        //    var sql = new Sql();
        //    sql.Append("SELECT dataNvarchar as postalCode, count (dataNvarchar) as locationCount FROM uLocate_LocationPropertyData WHERE LocationTypePropertyKey = '" + key + "' GROUP BY dataNvarchar");


        //    var finalResult = Repositories.ThisDb.Fetch<string>(sql).ToString();

        //    return finalResult;
        //}

        internal List<IndexedLocation> GetPaged(long PageNumber, long ItemsPerPage, string WhereClause)
        {
            Sql sql = new Sql();
            sql.Append(
                "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY [Key]) AS Num, * FROM uLocate_Location) AS uLocateTable");
            sql.Append(
                "WHERE Num BETWEEN ((@PageNumber - 1) * @RowspPage + 1) AND (@PageNumber * @RowspPage)",
                new { PageNumber = PageNumber, RowspPage = ItemsPerPage });
            sql.Append("ORDER BY [Key];");

            CurrentCollection.Clear();
            var dtoResult = Repositories.ThisDb.Fetch<LocationDto>(sql).ToList();

            var converter = new DtoConverter();
            CurrentCollection.AddRange(converter.ToLocationEntity(dtoResult));

            FillChildren();
            var ReturnList = new List<IndexedLocation>();

            foreach (var loc in CurrentCollection)
            {
                ReturnList.Add(new IndexedLocation(loc));
            }

            return ReturnList;
        }
        
        #endregion


        #region Reusable Functions

        private bool GeogIsValid(EditableLocation entity)
        {
            bool Result = false;

            if (entity.Latitude == 0 || entity.Longitude == 0)
            {
                return Result;
            }
            else
            {
                var Lat = Math.Round(entity.Latitude, 5);
                var Long = Math.Round(entity.Longitude, 5);

                var sql = new Sql();
                sql.Append("SELECT TOP 1 [GeogCoordinate].Lat AS DbLat, [GeogCoordinate].Long AS DbLong");
                sql.Append("FROM [uLocate_Location]");
                sql.Append(string.Format("WHERE  ([Key] = '{0}')", entity.Key));

                var DbGeogLatLong = Repositories.ThisDb.Query<dynamic>(sql).FirstOrDefault();
                if (DbGeogLatLong != null)
                {
                    var DbLat = Math.Round(DbGeogLatLong.DbLat, 5);
                    var DbLong = Math.Round(DbGeogLatLong.DbLong, 5);

                    if (Lat == DbLat & Long == DbLong)
                    {
                        Result = true;
                    }
                }

                return Result;
            }
        } 

        #endregion

        #endregion

        #region Protected Methods

        protected override IEnumerable<EditableLocation> PerformGetAll(params Guid[] Keys)
        {
            //TODO: Fix this - use cache + Dto
            List<EditableLocation> Result = new List<EditableLocation>();
            IEnumerable<LocationDto> dtoResults;

            if (Keys.Any())
            {
                foreach (var key in Keys)
                {
                    Result.Add(Get(key));
                }
            }
            else
            {
                var sql = new Sql();
                sql.Select("*").From<LocationDto>();

                dtoResults = Repositories.ThisDb.Fetch<LocationDto>(sql).ToList();

                var converter = new DtoConverter();
                foreach (var result in dtoResults)
                {
                    Result.Add(converter.ToLocationEntity(result));
                }
            }

            return Result;
        }

        protected override EditableLocation PerformGet(Guid Key)
        {
            var sql = new Sql();
            sql
                .Select("*")
                .From<LocationDto>()
                .Where<LocationDto>(n => n.Key == Key);

            var dtoResult = Repositories.ThisDb.Fetch<LocationDto>(sql).FirstOrDefault();

            if (dtoResult == null)
                return null;

            var converter = new DtoConverter();
            var entity = converter.ToLocationEntity(dtoResult);

            return entity;
        }

        protected override void PersistNewItem(EditableLocation item)
        {
            string Msg = string.Format("Location '{0}' has been saved.", item.Name);

            item.AddingEntity();
            this.SyncProperties(item);

            var converter = new DtoConverter();
            var dto = converter.ToLocationDto(item);

            Repositories.ThisDb.Insert(dto);
            //item.Key = dto.Key;

            LogHelper.Info(typeof(LocationRepository), Msg);

            PersistChildren(item);
        }

        protected override void PersistUpdatedItem(EditableLocation item)
        {
            string Msg = string.Format("Location '{0}' has been updated.", item.Name);

            item.UpdatingEntity();

            var converter = new DtoConverter();
            var dto = converter.ToLocationDto(item);

            Repositories.ThisDb.Update(dto);

            foreach (var prop in item.PropertyData)
            {
                prop.UpdatingEntity();
                var pDto = converter.ToLocationPropertyDataDto(prop);
                Repositories.ThisDb.Update(pDto);
            }

            this.UpdateDbGeography(item);

            LogHelper.Info(typeof(LocationRepository), Msg);
        }

        protected override void PersistDeletedItem(EditableLocation item, out StatusMessage StatusMsg)
        {
            StatusMessage ReturnMsg = new StatusMessage();
            ReturnMsg.ObjectName = item.Name;

            DeleteChildren(item);
            ReturnMsg.Message = string.Format("LocationType '{0}' has been deleted.", ReturnMsg.ObjectName);

            var converter = new DtoConverter();
            var dto = converter.ToLocationDto(item);

            Repositories.ThisDb.Delete(dto);
            ReturnMsg.Success = true;

            StatusMsg = ReturnMsg;
            //LogHelper.Info(typeof(LocationTypeRepository), ReturnMsg.Message);
        }

        protected override Sql GetBaseQuery(bool isCount)
        {
            var MySql = new Sql();
            MySql
                .Select(isCount ? "COUNT(*)" : "*")
                .From<LocationDto>();
            // .InnerJoin<LocationType>()
            // .On<LocationDto, LocationType>(left => left.LocationTypeId, right => right.Id);

            return MySql;
        }

        /// <summary>
        /// Gets the base where clause
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        protected override string GetBaseWhereClause()
        {
            return " WHERE Key= @0";
            //return "ulocateLocation.pk = @Key";
        }

        protected override IEnumerable<string> GetDeleteClauses()
        {
            var list = new List<string>
            {
                "DELETE FROM ulocateLocation WHERE pk = @Key"
            };

            return list;
        }

        #endregion

        #region Private Methods
        private void FillChildren()
        {
            this.FillProperties();
            
        }

        private void PersistChildren(EditableLocation item)
        {
            this.PersistProperties(item);
        }

        private void DeleteChildren(EditableLocation item)
        {
            this.DeleteProperties(item);
        }

        private void FillProperties()
        {
            //var LtpRepo = Repositories.LocationTypePropertyRepo;
            var LocDataRepo  = Repositories.LocationPropertyDataRepo;

            foreach (var Location in CurrentCollection)
            {
                Location.PropertyData = LocDataRepo.GetByLocation(Location.Key).ToList();
            }
        }

        private void PersistProperties(EditableLocation item)
        {
            this.SyncProperties(item);
            var Repo = Repositories.LocationPropertyDataRepo;
            foreach (var NewProp in item.PropertyData)
            {
                if (NewProp.LocationKey == Guid.Empty)
                {
                    NewProp.LocationKey = item.Key;
                }

                Repo.Insert(NewProp);
            }
        }

        private void DeleteProperties(EditableLocation item)
        {
            var Repo = new LocationPropertyDataRepository(Repositories.ThisDb, Helper.ThisCache);
            var MatchingProps = Repo.GetByLocation(item.Key);

            foreach (var Prop in MatchingProps)
            {
                Repo.Delete(Prop);
            }
        }

        private void SyncProperties(EditableLocation item)
        {
            //Get location type properties
            var AllTypeProperties = Repositories.LocationTypePropertyRepo.GetByLocationType(item.LocationTypeKey);
            
            //Get location property data
            List<LocationPropertyData> AllData = item.PropertyData.ToList();
            List<Guid> PropsToAdd = new List<Guid>();
            //compare type properties with location data
            foreach (var typeProperty in AllTypeProperties)
            {
                var MatchingProp = AllData.Where(p => p.LocationTypePropertyKey == typeProperty.Key);
                if (!MatchingProp.Any())
                {
                    PropsToAdd.Add(typeProperty.Key);
                }
            }

            foreach (var propKey in PropsToAdd)
            {
                var NewProp = new LocationPropertyData(item.Key, propKey);
                AllData.Add(NewProp);
            }


            //Add missing properties to Location with blank values

        }


        #endregion

        public override Page<EditableLocation> Page(long page, long itemsPerPage, Sql sql)
        {
            throw new NotImplementedException();
        }

    }
}