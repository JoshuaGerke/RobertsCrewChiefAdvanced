import xml.etree.ElementTree as ET

def sort_xml_elements(xml_file, output_file):
    # Parse the XML file
    tree = ET.parse(xml_file)
    root = tree.getroot()

    # Find the specific section CrewChiefV4.Properties.Settings
    settings_section = root.find(".//userSettings/CrewChiefV4.Properties.Settings")

    if settings_section is not None:
        print("Found")
        print(settings_section[0].tag[0])
        # Sort the elements in the settings section, ignoring case
        sorted_elements = sorted(settings_section, key=lambda elem: elem.get('name', '').lower())
        print(len(sorted_elements))

        # Remove all existing elements in the settings section
        for elem in list(settings_section):
            settings_section.remove(elem)

        # Append the sorted elements back to the settings section
        for elem in sorted_elements:
            settings_section.append(elem)

    # Write the updated XML to the output file
    tree.write(output_file, encoding='utf-8', xml_declaration=True)

# Example usage:
input_file = 'App.config'
output_file = 'sorted_App.config'
sort_xml_elements(input_file, output_file)

