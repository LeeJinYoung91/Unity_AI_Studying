using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FilterInputTest : MonoBehaviour
{
    [SerializeField] BadWordFilter filter;
    private TMP_InputField inputField;

    void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
        inputField.onSubmit.AddListener(OnSumit);
    }

    private async void OnSumit(string value)
    {
        bool isBad = await filter.IsBadWord(value);

        Debug.Log($"{value} is Bad?: {isBad}");
    }
}
