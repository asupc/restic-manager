namespace ResticManager.Models
{
    public class BaseModel
    {
        public int Id { get; set; }
    }

    public class BaseModel<T>
    {
        public T Id { get; set; }
    }
}
