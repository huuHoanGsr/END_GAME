using UnityEngine;
using UnityEngine.UI;

public class ResultUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject gameplayPanel; // Để tắt màn hình chơi game đi
    [SerializeField] private Text categoryNameText;
    [SerializeField] private Text specificMajorsText;

    //[Header("Managers")]
    //[SerializeField] private ScoreProfileManager scoreManager; // Kéo script tính điểm vào đây

    private void OnEnable()
    {
        // Lắng nghe sự kiện hết bài từ DeckManager
        //DeckManager.OnDeckEmpty += ShowResult;
    }

    private void OnDisable()
    {
        //DeckManager.OnDeckEmpty -= ShowResult;
    }

    private void ShowResult()
    {
        // 1. Lấy ra khối ngành điểm cao nhất
        //MajorCategory bestCategory = scoreManager.GetBestMatchedMajor();

        // 2. Tắt màn chơi, Bật màn kết quả
        gameplayPanel.SetActive(false);
        resultPanel.SetActive(true);

        // 3. Hiển thị thông tin lên UI
        //DisplayCategoryInfo(bestCategory);
    }

    private void DisplayCategoryInfo(MajorCategory category)
    {
        // Sử dụng Switch-Case (hoặc Dictionary) để in ra kết quả tương ứng
        switch (category)
        {
            case MajorCategory.InformationTechnology:
                categoryNameText.text = "KHỐI NGÀNH CÔNG NGHỆ THÔNG TIN";
                specificMajorsText.text = "Các chuyên ngành chờ đón bạn:\n- Lập trình Web\n- Lập trình Mobile\n- Lập trình Game\n- Ứng dụng AI\n- Phát triển phần mềm";
                break;
            case MajorCategory.Engineering:
                categoryNameText.text = "KHỐI NGÀNH KỸ THUẬT";
                specificMajorsText.text = "Các chuyên ngành chờ đón bạn:\n- Công nghệ Ô tô\n- Công nghệ Kỹ thuật Cơ khí\n- Tự động hóa\n- Chip & Bán dẫn";
                break;
            case MajorCategory.BusinessAndMarketing:
                categoryNameText.text = "KHỐI NGÀNH KINH TẾ & KINH DOANH";
                specificMajorsText.text = "Các chuyên ngành chờ đón bạn:\n- Digital Marketing\n- Logistics\n- Kế toán doanh nghiệp\n- Tổ chức sự kiện";
                break;
            case MajorCategory.TourismAndHospitality:
                categoryNameText.text = "DU LỊCH & KHÁCH SẠN";
                specificMajorsText.text = "Các chuyên ngành chờ đón bạn:\n- Quản trị Khách sạn\n- Quản trị Dịch vụ Du lịch & Lữ hành";
                break;
            case MajorCategory.Languages:
                categoryNameText.text = "KHỐI NGÀNH NGÔN NGỮ";
                specificMajorsText.text = "Các chuyên ngành chờ đón bạn:\n- Tiếng Anh\n- Tiếng Trung\n- Tiếng Hàn\n- Tiếng Nhật";
                break;
            case MajorCategory.Design:
                categoryNameText.text = "THIẾT KẾ ĐỒ HỌA";
                specificMajorsText.text = "Xưởng sáng tạo của FPT Poly đang chờ bạn!";
                break;
            case MajorCategory.Healthcare:
                categoryNameText.text = "KHỐI NGÀNH DƯỢC";
                specificMajorsText.text = "Bạn sinh ra để chăm sóc sức khỏe cho mọi người!";
                break;
        }
    }

    // Hàm này gắn vào nút Button "Chơi Lại" trên UI
    public void RestartGame()
    {
        // Load lại Scene hiện tại nhanh nhất (Nhớ using UnityEngine.SceneManagement;)
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}