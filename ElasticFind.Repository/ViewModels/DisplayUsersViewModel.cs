namespace ElasticFind.Repository.ViewModels;

public class DisplayUsersViewModel
{
    public PaginationViewModel? PaginationViewModel { get; set; }
    public List<UserViewModel> UserList { get; set; } = new List<UserViewModel>();
}
