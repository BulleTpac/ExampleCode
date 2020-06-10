using System;
using System.Linq;

namespace WebService.Cars
{
    public class ActiveCarFinderResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public ActiveCarFinderResult(bool success, string errorMessage = null)
        {
            this.Success = success;
            this.ErrorMessage = errorMessage;
        }
    }
    public class ActiveCarFinder
    {
        private ProjEntities db;
        private string removeSpaces(string inputString)
        {
            inputString = inputString.Trim().Replace(" ", string.Empty);
            return inputString;
        }
        protected Lazy<string[]> carPartnerIds;
        public ActiveCarFinder(ProjEntities db)
        {
            this.db = db;
            this.carPartnerIds = new Lazy<string[]>(() => this.db.Partners.Where(A => A.Name == "Wolksvagen" || A.Name == "BMW").Select(A => A.Id).ToArray());
        }
        private IQueryable<MAIN_BASE> filterByPartner(MAIN_BASE car, IQueryable<MAIN_BASE> cars)
        {
            string[] PartnerIds = this.PartnerIds.Value;
            return cars.Where(A => PartnerIds.Contains(A.PartnerId));
        }
        private IQueryable<MAIN_BASE> filterByStatus(MAIN_BASE car, IQueryable<MAIN_BASE> cars)
        {
            return cars.Where(A => A.Status == CarStateModel.ReleasedState.Name || A.Status == CarStateModel.Track);
        }
        private IQueryable<MAIN_BASE> filterByDate(MAIN_BASE car, IQueryable<MAIN_BASE> cars, DateTime? dateBegin, DateTime? dateEnd)
        {
            if (car.DateBegin.HasValue && car.DateEnd.HasValue)
            {

                return cars.Where(A => (car.DateBegin <= A.DateEnd && car.DateEnd >= A.DateBegin));
            }
            else
            {
                return cars.Where(A => (dateBegin <= A.DateEnd && dateEnd >= A.DateBegin));
            }
        }
        private IQueryable<MAIN_BASE> filterByCar(MAIN_BASE car, IQueryable<MAIN_BASE> cars)
        {
            return cars.Where(A => A.Cars.ViewName == car.Auto.ViewName);
        }
        private IQueryable<MAIN_BASE> filterObjectAddress(MAIN_BASE sourceCar, IQueryable<MAIN_BASE> cars)
        {
            var hashes = sourceCar
                .MAIN_BASEAddress
                .Where(address =>
                    address.TypeAddress == null &&
                    address.TypeRegister == true &&
                    !string.IsNullOrEmpty(address.TypeObject)
                )
                .Select(address => address.Md5)
                .ToArray();

            return cars.Where(car => car.MAIN_BASEAddress.Any(
                address =>
                    address.TypeAddress == null &&
                    address.TypeRegister == true &&
                    !string.IsNullOrEmpty(address.TypeObject) &&
                    hashes.Contains(address.Md5)
            ));
        }
        internal ActiveCarFinderResult CreateCar(ExtendedCarModel extendedCar)
        {
            var today = DateTime.Today;

            var cars = this.db.MAIN_BASE.AsQueryable();

            cars = this.filterByStatus(extendedCar.OrmModel, cars);

            if (cars.Any(car => car.Id != extendedCar.OrmModel.Id))
            {
                return new ActiveCarFinderResult(false, $"Минимум одна машина");
            }

            return new ActiveCarFinderResult(true);
        }
        public ActiveCarFinderResult AllowCreateCar(string carId, DateTime? dateBegin, DateTime? dateEnd)
        {
            MAIN_BASE car = this.db.MAIN_BASE.Find(carId);

            if (!this.PartnerIds.Value.Contains(car.PartnerId))
            {
                return new ActiveCarFinderResult(true);
            }

            var cars = this.db.MAIN_BASE.AsQueryable();

            cars = this.filterByPartner(car, cars);

            cars = this.filterByDate(car, cars, dateBegin, dateEnd);

            cars = this.filterByStatus(car, cars);

            string carName = car.Cars.Name;
            string carViewName = car.Cars.ViewName;

            if (carViewName.StartsWith("Volksvagen", StringComparison.OrdinalIgnoreCase)
                || carViewName.Equals("BMW", StringComparison.OrdinalIgnoreCase))
            {
                if (cars.Where(x => x.Id != carId).Count() > 2)
                {
                    return new ActiveCarFinderResult(false, $"Машин больше 3ех {carViewName}. в базе");
                }
            }
            else if (carViewName.Equals("Tayota", StringComparison.OrdinalIgnoreCase))
            {
                var pers = car.MAIN_BASEPersons.Single(person => person.PersonType == true);
                string inn = (pers.INN ?? "").Replace(" ", "").Trim();
                if (string.Equals(pers.Category, "Юр. лицо", StringComparison.OrdinalIgnoreCase))
                {
                    cars = cars.Where(pol => pol.MAIN_BASEPersons.Any(per => per.PersonType == true && (per.INN ?? "").Replace(" ", "").Trim() == inn));
                }
                else
                {
                    string lastName = (pers.LastName ?? "").Replace(" ", "").Trim();
                    string firstName = (pers.FirstName ?? "").Replace(" ", "").Trim();
                    string middleName = (pers.MiddleName ?? "").Replace(" ", "").Trim();
                    cars = cars.Where(pol => pol.MAIN_BASEPersons.Any(per => per.PersonType == true &&
                        (per.LastName ?? "").Replace(" ", "").Trim() == lastName &&
                        (per.FirstName ?? "").Replace(" ", "").Trim() == firstName &&
                        (per.MiddleName ?? "").Replace(" ", "").Trim() == middleName &&
                        per.DateBorn == pers.DateBorn));
                }
                var existedPersCount = cars.SelectMany(A => A.MAIN_BASEPersons.Where(B => B.PersonType == false)).Count();
                var addedPersCount = car.MAIN_BASEPersons.Where(per => per.PersonType == false).Count();
                if (existedPersCount + addedPersCount > 100)
                {
                    return new ActiveCarFinderResult(false, $"У указанного человека {existedPersCount} есть авто {carViewName}");
                }
            }

            return new ActiveCarFinderResult(true);
        }
    }
}