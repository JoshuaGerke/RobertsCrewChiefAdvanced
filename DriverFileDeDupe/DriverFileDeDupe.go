// Program to search for duplicates in CrewChiefV4/sounds/driver_names
// delete them and add the duplicate to duplicate_names.txt
// Written in Go because... why not? ;-)
package main

import (
	"fmt"
	"io/ioutil"
	"os"
	"path/filepath"
	"sort"
	"strings"
)

func getFileSize(path string) (int64, error) {
	fileInfo, err := os.Stat(path)
	if err != nil {
		return 0, err
	}

	return fileInfo.Size(), nil
}

func compareFiles(file1, file2 string) (bool, error) {
	size1, err := getFileSize(file1)
	if err != nil {
		return false, err
	}

	size2, err := getFileSize(file2)
	if err != nil {
		return false, err
	}

	if size1 != size2 {
		return false, nil
	}

	content1, err := ioutil.ReadFile(file1)
	if err != nil {
		return false, err
	}

	content2, err := ioutil.ReadFile(file2)
	if err != nil {
		return false, err
	}

	return string(content1) == string(content2), nil
}

// FileItem represents a file with its name and size
type FileItem struct {
	Name string
	Size int64
}

func main() {
	// Provide the folder path to compare files
	folderPath := "../CrewChiefV4/sounds/driver_names"

	// Create the duplicate_names.txt file
	duplicateNamesFile, err := os.OpenFile(filepath.Join(folderPath, "duplicate_names.txt"),
		os.O_WRONLY|os.O_CREATE|os.O_TRUNC, 0644)
	if err != nil {
		fmt.Println("Error creating duplicate_names.txt:", err)
		return
	}
	defer duplicateNamesFile.Close()

	files, err := ioutil.ReadDir(folderPath)
	if err != nil {
		fmt.Printf("Error reading directory: %v\n", err)
		return
	}

	// Create a slice of FileItem to store file information
	fileItems := make([]FileItem, 0, len(files))

	// Iterate over the files and get their sizes
	for _, file := range files {
		filePath := filepath.Join(folderPath, file.Name())
		size, err := getFileSize(filePath)
		if err != nil {
			fmt.Printf("Error getting file size: %v\n", err)
			continue
		}

		fileItems = append(fileItems, FileItem{
			Name: file.Name(),
			Size: size,
		})
	}

	// Sort the fileItems slice by size in ascending order
	sort.Slice(fileItems, func(i, j int) bool {
		return fileItems[i].Size < fileItems[j].Size
	})

	firstInstance := 0
	compareTo := 1
	for compareTo < len(fileItems) {
		firstInstancePath := filepath.Join(folderPath, fileItems[firstInstance].Name)
		_, err := os.Stat(firstInstancePath)
		if err != nil {
			if os.IsNotExist(err) {
				firstInstance++
				continue
			}
		}

		compareToPath := filepath.Join(folderPath, fileItems[compareTo].Name)
		if filepath.Ext(firstInstancePath) != ".wav" || filepath.Ext(compareToPath) != ".wav" {
			firstInstance++
			compareTo++
			continue
		}

		match, err := compareFiles(firstInstancePath, compareToPath)
		if err != nil {
			break
		}
		if match {
			pathName := strings.TrimSuffix(filepath.Base(firstInstancePath), filepath.Ext(firstInstancePath))
			fileName := strings.TrimSuffix(filepath.Base(compareToPath), filepath.Ext(compareToPath))
			fmt.Printf("Files with same contents:\n%s\n%s\n", pathName, fileName)

			// Delete the duplicate file
			err := os.Remove(compareToPath)
			if err != nil {
				break
			}
			// Write the pair of duplicate file names to the duplicate_names.txt file
			_, err = duplicateNamesFile.WriteString(fmt.Sprintf("%s:%s\n", pathName, fileName))
			if err != nil {
				fmt.Println("Error writing to duplicate_names.txt:", err)
				break
			}
			compareTo++
		} else {
			firstInstance++
			compareTo++
		}
	}
}
