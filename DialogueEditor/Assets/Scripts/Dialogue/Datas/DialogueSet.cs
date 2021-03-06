﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq; 
using UnityEditor;

namespace DialogueEditor
{
    [Serializable]
    public class DialogueSet : DialogueNode
    {
        #region Fields and Properties
        [SerializeField] private List<DialogueLine> m_dialogLines = new List<DialogueLine>();
        [SerializeField] private DialogSetType m_type = DialogSetType.BasicType;
        [SerializeField] private bool m_playOnlyOneLine = false;
        [SerializeField] private bool m_playRandomly = false;
        private int[] m_unusedIndexes = null;

        public List<DialogueLine> DialogLines { get { return m_dialogLines; } }
        public DialogSetType Type { get { return m_type; } }
        public bool PlayOnlyOneLine { get { return m_playOnlyOneLine; } }
        public bool PlayRandomly { get { return m_playRandomly; } }
        public int RemainingIndexesCount
        {
            get
            {
                if (m_unusedIndexes == null) return 0;
                return m_unusedIndexes.Length;
            }
        }



#if UNITY_EDITOR
        private Action<DialogueSet> m_onRemoveDialogPart = null;
        private GUIContent m_basicSetIcon = null;
        private GUIContent m_answerIcon = null;
#endif
        #endregion


        #region Constructor
#if UNITY_EDITOR
        public DialogueSet(Vector2 _nodePosition, Action<DialogueSet> _onRemovePart, GUIStyle _normalStyle, GUIStyle _selectedStyle, GUIStyle _connectionPointStyle, GUIContent _dialogPartIcon, GUIContent _answerIcon, GUIContent _pointIcon)
        {
            m_NodeToken = UnityEngine.Random.Range(0, int.MaxValue);
            m_nodeRect = new Rect(_nodePosition.x, _nodePosition.y, INITIAL_NODE_WIDTH, 0);
            m_onRemoveDialogPart = _onRemovePart;
            m_nodeStyle = _normalStyle;
            m_selectedNodeStyle = _selectedStyle;
            m_connectionPointStyle = _connectionPointStyle;
            m_dialogLines = new List<DialogueLine>();
            m_basicSetIcon = _dialogPartIcon;
            m_answerIcon = _answerIcon;
            m_currentIcon = m_type == DialogSetType.BasicType ? m_basicSetIcon : m_answerIcon;
            m_pointIcon = _pointIcon;
            AddNewContent();
        }
#endif
        #endregion


        #region Methods
#if UNITY_EDITOR
        /// <summary>
        /// Add new content to this part
        /// </summary>
        private void AddNewContent()
        {
            if (m_dialogLines == null) m_dialogLines = new List<DialogueLine>();
            if (m_type == DialogSetType.BasicType && m_dialogLines.Count > 0)
            {
                m_dialogLines.Last().LinkedToken = -1;
            }
            m_dialogLines.Add(new DialogueLine());
            m_nodeRect = new Rect(m_nodeRect.position.x,
                m_nodeRect.position.y,
                INITIAL_NODE_WIDTH,
                INITIAL_NODE_HEIGHT + (TITLE_HEIGHT * 2) + SPACE_HEIGHT + (DIALOGLINE_SETTINGS_HEIGHT * m_dialogLines.Count) + (SPACE_HEIGHT * (m_dialogLines.Count + 1)) + BUTTON_HEIGHT);
        }

        /// <summary>
        /// Change the type of the dialog part and modify the GUI according to the new type
        /// </summary>
        /// <param name="_type"></param>
        private void ChangeType(DialogSetType _type)
        {
            m_type = _type;
            switch (m_type)
            {
                case DialogSetType.BasicType:
                    m_currentIcon = m_basicSetIcon;
                    for (int i = 0; i < m_dialogLines.Count - 1; i++)
                    {
                        m_dialogLines[i].LinkedToken = -1;
                    }
                    break;
                case DialogSetType.PlayerAnswer:
                    m_currentIcon = m_answerIcon;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Draw the Dialog Set Editor
        /// </summary>
        /// <param name="_lineDescriptor">Line Descriptor</param>
        /// <param name="_otherSets">The other dialog sets</param>
        /// <param name="_onOutDialogLineSelected">Action called when an out point is selected</param>
        /// <param name="_onInDialogNodeSelected">Action called when the In point of the Dialog set is selected</param>
        public void Draw(string _lineDescriptor, List<DialogueSet> _otherSets, List<DialogueCondition> _otherConditions, Action<DialogueLine> _onOutDialogLineSelected, Action<DialogueNode> _onInDialogNodeSelected, List<CharacterColorSettings> _colorSettings)
        {

            // --- Draw the connections between the parts --- //
            if (GUI.Button(InPointRect, m_pointIcon, m_connectionPointStyle))
            {
                _onInDialogNodeSelected.Invoke(this);
            }

            // --- Draw the Set and its Lines --- //
            GUI.Box(m_nodeRect, "", IsSelected ? m_selectedNodeStyle : m_nodeStyle);
            Rect _r = new Rect(m_nodeRect.position.x + m_nodeRect.width - 35, m_nodeRect.position.y + MARGIN_HEIGHT, 25, 25);
            if (GUI.Button(_r, m_currentIcon, m_nodeStyle))
            {
                ProcessContextMenu();
            }
            _r = new Rect(m_nodeRect.x + 10, _r.y, CONTENT_WIDTH, TITLE_HEIGHT);
            GUI.Label(_r, m_type.ToString());
            _r.y = m_nodeRect.y + INITIAL_NODE_HEIGHT + SPACE_HEIGHT / 2;
            EditorGUI.BeginDisabledGroup(m_type == DialogSetType.PlayerAnswer);
            m_playOnlyOneLine = EditorGUI.ToggleLeft(_r, "Play only one line of the set.", m_playOnlyOneLine);
            _r.y += TITLE_HEIGHT;
            m_playRandomly = EditorGUI.ToggleLeft(_r, "Play the set Randomly?", m_playRandomly);
            _r.y += TITLE_HEIGHT;
            EditorGUI.EndDisabledGroup();
            Color _color = GUI.color;
            GUI.color = Color.black;
            GUI.Box(new Rect(_r.x, _r.y, _r.width, .5f), "");
            GUI.color = _color;
            _r.y += SPACE_HEIGHT / 2;

            DialogueLine _c;
            for (int i = 0; i < m_dialogLines.Count; i++)
            {
                _c = m_dialogLines[i];
                _r.y = _c.Draw(_r.position, _lineDescriptor, RemoveContent, m_type, (m_type == DialogSetType.BasicType && i == m_dialogLines.Count - 1), m_pointIcon, m_connectionPointStyle, _onOutDialogLineSelected, _otherSets, _otherConditions, _colorSettings);
            }
            _r = new Rect(_r.position.x, _r.y, _r.width, BUTTON_HEIGHT);
            if (GUI.Button(_r, "Add new Dialog Line"))
            {
                AddNewContent();
            }
        }

        /// <summary>
        /// Call the event "m_onRemoveDialogPart" with argument as itself
        /// -> Remove this set from the Dialog 
        /// </summary>
        private void OnClickRemoveNode()
        {
            m_onRemoveDialogPart?.Invoke(this);
        }

        /// <summary>
        /// Display the Context menu on the selected DialogPart
        /// </summary>
        protected override void ProcessContextMenu()
        {
            IsSelected = true;
            GenericMenu _genericMenu = new GenericMenu();
            switch (m_type)
            {
                case DialogSetType.BasicType:
                    _genericMenu.AddDisabledItem(new GUIContent("Set as Basic Dialog Part"));
                    _genericMenu.AddItem(new GUIContent("Set as Answer Dialog Part"), false, () => ChangeType(DialogSetType.PlayerAnswer));
                    break;
                case DialogSetType.PlayerAnswer:
                    _genericMenu.AddItem(new GUIContent("Set as Basic Dialog Part"), false, () => ChangeType(DialogSetType.BasicType));
                    _genericMenu.AddDisabledItem(new GUIContent("Set as Answer Dialog Part"));
                    break;
                default:
                    break;
            }
            _genericMenu.AddSeparator("");
            _genericMenu.AddItem(new GUIContent("Remove Node"), false, OnClickRemoveNode);
            _genericMenu.ShowAsContext();
        }

        /// <summary>
        /// Remove the selected Content and rescale the Rect
        /// </summary>
        /// <param name="_content"></param>
        private void RemoveContent(DialogueLine _content)
        {
            m_dialogLines.Remove(_content);
            m_nodeRect = new Rect(m_nodeRect.position.x, m_nodeRect.position.y,
                INITIAL_NODE_WIDTH,
                INITIAL_NODE_HEIGHT + (TITLE_HEIGHT * 2) + SPACE_HEIGHT + (DIALOGLINE_SETTINGS_HEIGHT * m_dialogLines.Count) + (SPACE_HEIGHT * (m_dialogLines.Count + 1)) + BUTTON_HEIGHT);

        }

        /// <summary>
        /// Initialize the Editor Settings for the part
        /// </summary>
        /// <param name="_nodeStyle">The Node Style of the Set</param>
        /// <param name="_connectionPointStyle">Style of the connection point</param>
        /// <param name="_dialogSetIcon">Icon of the Basic Dailog set</param>
        /// <param name="_answerIcon">Icon of the Answer Dialog Set</param>
        /// <param name="_startingSetIcon">Icon of the Starting Set</param>
        /// <param name="_pointIcon">Icon of the in/out points</param>
        /// <param name="_onRemoveSet">Action Called to remove the Set from the Dialog</param>
        /// <param name="_setStartingSet">Action called when the set is switch as the starting set</param>
        public void InitEditorSettings(GUIStyle _nodeStyle, GUIStyle _selectedNodeStyle, GUIStyle _connectionPointStyle, GUIContent _dialogSetIcon, GUIContent _answerIcon, GUIContent _pointIcon, Action<DialogueSet> _onRemoveSet)
        {
            m_nodeStyle = _nodeStyle;
            m_selectedNodeStyle = _selectedNodeStyle;
            m_onRemoveDialogPart = _onRemoveSet;
            m_basicSetIcon = _dialogSetIcon;
            m_answerIcon = _answerIcon;
            m_currentIcon = m_type == DialogSetType.BasicType ? m_basicSetIcon : m_answerIcon;
            m_connectionPointStyle = _connectionPointStyle;
            m_pointIcon = _pointIcon;
        }
#endif

        public int GetNextRandomIndex()
        {
            if (m_unusedIndexes == null)
            {
                m_unusedIndexes = new int[m_dialogLines.Count];
                for (int i = 0; i < m_dialogLines.Count; i++)
                {
                    m_unusedIndexes[i] = i;
                }
            }
            if (m_unusedIndexes.Length == 0)
            {
                m_unusedIndexes = null;
                return -1;
            }
            int _index = UnityEngine.Random.Range(0, m_unusedIndexes.Length);
            int _returnedValue = m_unusedIndexes[_index];

            List<int> _temp = m_unusedIndexes.ToList();
            _temp.RemoveAt(_index);
            m_unusedIndexes = _temp.ToArray();
            return _returnedValue;
        }
        #endregion

    }
}