import sys

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Add SwitchCharacters method to RadarForm
    if 'public void SwitchCharacters()' not in content:
        switch_method = """
        public void SwitchCharacters()
        {
            lock (dataLock)
            {
                var temp = char1;
                char1 = char2;
                char2 = temp;
            }
        }
"""
        content = content.replace(
            'public void SaveConfig()',
            switch_method + '\n        public void SaveConfig()'
        )

    # Add the button to SettingsForm
    if 'Intercambiar Personaje Activo' not in content:
        btn_code = """
            Button btnSwitch = new Button();
            btnSwitch.Text = "Intercambiar Personaje Activo";
            btnSwitch.Location = new Point(15, yOffset);
            btnSwitch.Size = new Size(200, 30);
            btnSwitch.FlatStyle = FlatStyle.Flat;
            btnSwitch.Click += (s, e) => {
                radar.SwitchCharacters();
            };
            this.Controls.Add(btnSwitch);
            yOffset += 40;
"""
        content = content.replace(
            'chkRogue.CheckedChanged += (s, e) => { radar.Config.AntiRogueAlerts = chkRogue.Checked; radar.SaveConfig(); };',
            'chkRogue.CheckedChanged += (s, e) => { radar.Config.AntiRogueAlerts = chkRogue.Checked; radar.SaveConfig(); };\n' + btn_code
        )

    # Also increase SettingsForm size slightly to fit the button
    content = content.replace('this.Size = new Size(250, 370);', 'this.Size = new Size(250, 410);')

    with open(cs_path, 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
