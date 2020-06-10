internal class NonExportResultProvider : IExportResultProvider<ResolveCreateStatus>
    {
        public string ProviderName => "AddBox";

        public IQueryable<INTEGRATION> Filter(IQueryable<INTEGRATION> sourceRows)
        {
            return sourceRows.Where(row => !row.MAIN_BASE.Products.Name.Contains("Cars"));
        }

        public bool IsValid(INTEGRATION rowToExport)
        {
            return rowToExport.RequestXml.IsDeserializableXmlTo<Box>();
        }

        public GetResultOutput GetResult(MAIN_BASE item, INTEGRATION rowToExport)
        {
            var xml = rowToExport.RequestXml.DeserializeXmlTo<Box>();

            var result = new GetResultOutput();

            var partner = item.Partners;
            var product = item.Products;

            if (rowToExport.StatusExport == (int) StatusExportEnum.Ready)
            {
                using (var client = new Contract() { Timeout = 40000 })
                {
                    try
                    {
                        result.ResultAdd = client.AddBox(xml, false);

                        result.ResultStatus = result.ResultAdd.Status == CreatedStatus.ok ? StatusExportEnum.Exported : StatusExportEnum.Ready;
                    }
                    catch (Exception exception)
                    {
                        result.ResultAdd = new ResolveCreateStatus();

                        result.ResultAdd.Errors = new Errors[]
                        {
                            new Errors()
                            {
                                Error = "ошибка AddBox",
                                Value = exception.ToString()
                            }
                        };

                        result.ResultStatus = StatusExportEnum.Ready;

                        return result;
                    }
                };
            }

            if (partner.ViewName == "VolkSvagen" || partner.ViewName == "BMW")
            {
                if (rowToExport.StatusExport == (int) StatusExportEnum.Response || result.ResultAdd.Status == CreatedStatus.ok)
                {
                    result.Result = new Contract().Cars(result.ResultAdd.id);
                    result.ResultStatus = result.ResultCars.Status == true ? StatusExportEnum.Exported : StatusExportEnum.RegPolicyResponse;
                }
            }

            return result;
        }
    }