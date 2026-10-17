using System.Text;
using UnityEngine.SceneManagement;

public sealed partial class QuestJournalManager
{
    public const string KnownPasswordsEntryId = "level2.known_passwords";
    private bool tavernRoomPasswordKnown;
    private bool engineerSafePasswordKnown;
    private bool entertainmentRoomPasswordKnown;
    private bool warehouseGatePasswordKnown;
    private bool churchRelicPasswordKnown;
    private Entry knownPasswordsEntry;

    // Fixed story triggers only. Do not scan doors, parse prose, or record guesses.
    public void UnlockTavernRoomPassword()
    {
        if (tavernRoomPasswordKnown) return;
        tavernRoomPasswordKnown = true;
        JournalChanged?.Invoke();
    }

    public void UnlockEngineerSafePassword()
    {
        if (engineerSafePasswordKnown) return;
        engineerSafePasswordKnown = true;
        JournalChanged?.Invoke();
    }

    public void UnlockEntertainmentRoomPassword()
    {
        if (entertainmentRoomPasswordKnown) return;
        entertainmentRoomPasswordKnown = true;
        JournalChanged?.Invoke();
    }

    public void UnlockWarehouseGatePassword()
    {
        if (warehouseGatePasswordKnown) return;
        warehouseGatePasswordKnown = true;
        JournalChanged?.Invoke();
    }

    public void UnlockChurchRelicPassword()
    {
        if (churchRelicPasswordKnown) return;
        churchRelicPasswordKnown = true;
        JournalChanged?.Invoke();
    }

    private Entry GetKnownPasswordsEntry()
    {
        if (GetLevel2SceneStage(SceneManager.GetActiveScene().name) == 0) return null;
        var text = new StringBuilder();
        if (tavernRoomPasswordKnown)
            text.Append("酒馆二楼客房：9090\n\n");
        if (engineerSafePasswordKnown)
            text.Append("大工程师旁的保险柜（地下层）：1551\n\n");
        if (entertainmentRoomPasswordKnown)
            text.Append("娱乐中心左侧客房（一层）：9014\n\n");
        if (warehouseGatePasswordKnown)
            text.Append("一层仓库大门：9871\n\n");
        if (churchRelicPasswordKnown)
            text.Append("教堂二楼圣物间：7028（另需钥匙）\n\n");
        string details = text.Length == 0
            ? "尚未记录任何密码。\n\n阅读相关文档或通过对话得知密码后，会在此处记录。"
            : text.ToString().TrimEnd();
        if (knownPasswordsEntry == null)
            knownPasswordsEntry = new Entry(KnownPasswordsEntryId, "已知密码", details, "Level2-Floor1");
        else
            knownPasswordsEntry.Update(KnownPasswordsEntryId, "已知密码", details, "Level2-Floor1");
        return knownPasswordsEntry;
    }

    private void RestoreKnownPasswords(SaveState state)
    {
        tavernRoomPasswordKnown = state.tavernRoomPasswordKnown;
        entertainmentRoomPasswordKnown = state.entertainmentRoomPasswordKnown;
        warehouseGatePasswordKnown = state.warehouseGatePasswordKnown;
        churchRelicPasswordKnown = state.churchRelicPasswordKnown;
        // Older saves already remember accepting the engineer's explanation.
        // Merely meeting him (including the prerequisite/stranger greeting) does not qualify.
        engineerSafePasswordKnown = state.engineerSafePasswordKnown ||
            HasRememberedDialogueActor(SandwormMortarAgreementId);
        knownPasswordsEntry = null;
    }
}
