﻿using System.Collections;
using UnityEngine;
using TMPro;
using MoonSharp.Interpreter;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Events;

namespace DialogueEditor
{
    public class DialogueReader : MonoBehaviour
    {
        #region Fields and Property 
        [SerializeField] private string m_dialogName = "";
        [SerializeField] private TMP_Text m_textDisplayer = null;
        [SerializeField] private TMP_FontAsset m_font = null;
        [SerializeField] private float m_fontSize = 12;
        [SerializeField] private Color m_fontColor = Color.black;
        [SerializeField] private AudioSource m_audioSource = null;

        [SerializeField] private UnityEvent m_onStartReading = null;
        [SerializeField] private UnityEvent m_onEndReading = null; 

        private AsyncOperationHandle<TextAsset> m_dialogAssetAsyncHandler;

        private Dialogue m_dialog = null;
        private Script m_lineDescriptor = null;

        private System.Action m_onMouseClicked = null;

        public string DialogName
        {
            get
            {
                return m_dialogName;
            }
        }
        public UnityEventString OnDialogLineRead { get; private set; } = new UnityEventString();
        #endregion

        #region Methods

        #region Original Methods

        #region Load Dialog
        /// <summary>
        /// Initialize the settings of the text displayer
        /// </summary>
        private void InitReader()
        {
            if (m_textDisplayer)
            {
                if (m_font) m_textDisplayer.font = m_font;
                m_textDisplayer.fontSize = m_fontSize;
                m_textDisplayer.color = m_fontColor;
            }
        }

        /// <summary>
        /// Called when the DialogAsset is loaded
        /// Get the Dialog and Start loading the LineDescriptor
        /// </summary>
        /// <param name="_loadedAsset">The loaded asset Handler</param>
        private void OnDialogueAssetLoaded(AsyncOperationHandle<TextAsset> _loadedAsset)
        {
            if (_loadedAsset.Result == null)
            {
                Debug.LogError("IS NULL");
                return;
            }
            m_dialog = JsonUtility.FromJson<Dialogue>(_loadedAsset.Result.ToString());
        }

        /// <summary>
        /// Wait for the complete loading of the Line Descriptors Assets
        /// When they are loaded, get the Line Descriptor of the Dialog Asset
        /// </summary>
        /// <returns></returns>
        private void OnLineDescriptorLoaded()
        {
            if (DialogueAssetsManager.LineDescriptorsTextAsset.Any(a => a.name == m_dialog.SpreadSheetID.GetHashCode().ToString() + Dialogue.LineDescriptorPostfix))
            {
                m_lineDescriptor = new Script();
                m_lineDescriptor.DoString(DialogueAssetsManager.LineDescriptorsTextAsset.Where(a => a.name == m_dialog.SpreadSheetID.GetHashCode().ToString() + Dialogue.LineDescriptorPostfix).First().ToString());
            }
        }
        #endregion

        #region Display Dialogues lines
        /// <summary>
        /// Display the whole dialog
        /// </summary>
        /// <returns></returns>
        public void StartDisplayingDialogue(DialogStarterEnum _starter)
        {
            if (m_dialog == null)
            {
                Debug.Log("Dialog is null");
                return;
            }
            m_onStartReading?.Invoke();
            // Get the Starting Dialog Set //
            DialogueSet _set = m_dialog.GetFirstSet(_starter);
            DisplayDialogueSet(_set);
        }

        /// <summary>
        /// Call the method with the Situation Default
        /// </summary>
        public void StartDisplayingDialogue()
        {
            StartDisplayingDialogue(DialogStarterEnum.Default);
        }

        /// <summary>
        /// Display the dialog set according to its type
        /// </summary>
        /// <param name="_set"></param>
        /// <param name="_index"></param>
        private void DisplayDialogueSet(DialogueSet _set, int _index = 0)
        {
            if (_set == null)
            {
                m_onEndReading?.Invoke(); 
                m_textDisplayer.text = string.Empty;
                return;
            }
            switch (_set.Type)
            {
                case DialogSetType.BasicType:
                    if (_set.PlayRandomly) _index = _set.GetNextRandomIndex();
                    StartCoroutine(DisplayDialogueLineAtIndex(_set, _index));
                    break;
                case DialogSetType.PlayerAnswer:
                    DisplayAnswerDialogueSet(_set);
                    break;
            }
        }

        /// <summary>
        /// Instanciate the loaded asset <see cref="m_dialogAnswerHandler"/> and Initialise it using the dialog <paramref name="_set"/>
        /// </summary>
        /// <param name="_set">Displayed Set</param>
        private void DisplayAnswerDialogueSet(DialogueSet _set)
        {
            m_onMouseClicked = null;
            Transform _canvas = FindObjectOfType<Canvas>().transform;
            Instantiate(DialogueAssetsManager.DialogAnswerHandler, _canvas).GetComponent<DialogueAnswerHandler>().InitHandler(this, _set.DialogLines);
        }

        /// <summary>
        /// USED FROM A DIALOG ANSWER HANDLER
        /// Display the selected Dialog Line and procede to the next dialog set
        /// </summary>
        /// <param name="_line">The line to display</param>
        public IEnumerator DisplayDialogueLine(DialogueLine _line)
        {
            // Call the event
            OnDialogLineRead?.Invoke(_line.Key);
            // Change the color of the font if needed
            if (!DialoguesSettingsManager.DialogsSettings.OverrideCharacterColor)
            {
                if (DialoguesSettingsManager.DialogsSettings.CharactersColor.Any(c => c.CharacterIdentifier == _line.CharacterIdentifier))
                    m_textDisplayer.color = DialoguesSettingsManager.DialogsSettings.CharactersColor.Where(c => c.CharacterIdentifier == _line.CharacterIdentifier).Select(c => c.CharacterColor).FirstOrDefault();
                else m_textDisplayer.color = m_fontColor;
            }
            else
                m_textDisplayer.color = m_fontColor;
            // Change the text of the text displayer
            m_textDisplayer.text = GetDialogueLineContent(_line.Key, DialoguesSettingsManager.DialogsSettings.CurrentLocalisationKey);
            // If there is an audiosource and the AudioClip Exists in the DialogsAssetsManager, play the audioclip in OneShot
            if (m_audioSource != null && DialogueAssetsManager.DialogLinesAudioClips.ContainsKey(_line.Key + "_" + DialoguesSettingsManager.DialogsSettings.CurrentAudioLocalisationKey))
            {
                m_audioSource.PlayOneShot(DialogueAssetsManager.DialogLinesAudioClips[_line.Key + "_" + DialoguesSettingsManager.DialogsSettings.CurrentAudioLocalisationKey]);
            }
            yield return new WaitForSeconds(_line.InitialWaitingTime);
            // Go to the next set
            DialogueSet _nextSet = m_dialog.GetNextSet(_line.LinkedToken);
            DisplayDialogueSet(_nextSet);
        }

        /// <summary>
        /// Display the dialog line of the dialog set at the selected index
        /// </summary>
        /// <param name="_set">Displayed Dialog Set</param>
        /// <returns></returns>
        private IEnumerator DisplayDialogueLineAtIndex(DialogueSet _set, int _index = 0)
        {
            m_onMouseClicked = null;
            // Get the dialog line at the _index in the _set
            DialogueLine _line = _set.DialogLines[_index];
            // Call the event
            OnDialogLineRead?.Invoke(_line.Key);
            // Change the color of the font if needed
            if (!DialoguesSettingsManager.DialogsSettings.OverrideCharacterColor)
            {
                if (DialoguesSettingsManager.DialogsSettings.CharactersColor.Any(c => c.CharacterIdentifier == _line.CharacterIdentifier))
                    m_textDisplayer.color = DialoguesSettingsManager.DialogsSettings.CharactersColor.Where(c => c.CharacterIdentifier == _line.CharacterIdentifier).Select(c => c.CharacterColor).FirstOrDefault();
                else m_textDisplayer.color = m_fontColor;
            }
            else
                m_textDisplayer.color = m_fontColor;
            // Change the text of the text displayer
            m_textDisplayer.text = GetDialogueLineContent(_line.Key, DialoguesSettingsManager.DialogsSettings.CurrentLocalisationKey);
            // If there is an audiosource and the AudioClip Exists in the DialogsAssetsManager, play the audioclip in OneShot
            if (m_audioSource != null && DialogueAssetsManager.DialogLinesAudioClips.ContainsKey(_line.Key + "_" + DialoguesSettingsManager.DialogsSettings.CurrentAudioLocalisationKey))
            {
                AudioClip _c = DialogueAssetsManager.DialogLinesAudioClips[_line.Key + "_" + DialoguesSettingsManager.DialogsSettings.CurrentAudioLocalisationKey];

                m_audioSource.PlayOneShot(_c);
                yield return new WaitForSeconds(_c.length + .05f);
            }
            else
            {
                yield return new WaitForSeconds(_line.InitialWaitingTime);
            }

            // Increase Index
            _index++;
            //Check if we reach the end of the set and go to the next set
            if (_set.DialogLines.Count == _index && !_set.PlayRandomly)
            {
                _set = m_dialog.GetNextSet(_line.LinkedToken);
                _index = 0;
            }
            else if (_set.PlayOnlyOneLine || (_set.PlayRandomly && _set.RemainingIndexesCount == 0))
            {
                _set = m_dialog.GetNextSet(_set.DialogLines.Last().LinkedToken);
                _index = 0;
            }

            switch (_line.WaitingType)
            {
                case WaitingType.None:
                    DisplayDialogueSet(_set, _index);
                    break;
                case WaitingType.WaitForClick:
                    m_onMouseClicked += () => DisplayDialogueSet(_set, _index);
                    break;
                case WaitingType.WaitForTime:
                    StartCoroutine(WaitBeforeDisplayDialogueSet(_set, _index, _line.ExtraWaitingTime));
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Get the Content of the Dialog Line according to the localisationKey Selected
        /// </summary>
        /// <param name="_dialogLineID">ID of the Dialog Line</param>
        /// <param name="_localisationKey">Localisation Key to use</param>
        /// <returns></returns>
        public string GetDialogueLineContent(string _dialogLineID, string _localisationKey)
        {
            if (_dialogLineID == string.Empty) return string.Empty;
            DynValue _content = m_lineDescriptor.Globals.Get(_dialogLineID).Table.Get(_localisationKey);
            return _content.String;
        }

        /// <summary>
        /// Wait <paramref name="_waitingTime"/> seconds.
        /// Then Display the dialog line at the <paramref name="_index"/> Index of the <paramref name="_set"/>
        /// </summary>
        /// <param name="_set">Next set to display</param>
        /// <param name="_index">Index of the dialog line to display</param>
        /// <param name="_waitingTime">Time to wait before displaying</param>
        /// <returns></returns>
        private IEnumerator WaitBeforeDisplayDialogueSet(DialogueSet _set, int _index, float _waitingTime)
        {
            yield return new WaitForSeconds(_waitingTime);
            DisplayDialogueSet(_set, _index);
        }
        #endregion

        #endregion

        #region Unity Methods
        private void Awake()
        {
            if (m_dialogName != string.Empty)
            {
                m_dialogAssetAsyncHandler = Addressables.LoadAssetAsync<TextAsset>(m_dialogName);
                m_dialogAssetAsyncHandler.Completed += OnDialogueAssetLoaded;
            }
            DialogueAssetsManager.LineDescriptorsLoadedCallBack += OnLineDescriptorLoaded; 
            InitReader();
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
                m_onMouseClicked?.Invoke();
        }
        #endregion

        #endregion
    }

    [System.Serializable]
    public class UnityEventString : UnityEvent<string> { }
}