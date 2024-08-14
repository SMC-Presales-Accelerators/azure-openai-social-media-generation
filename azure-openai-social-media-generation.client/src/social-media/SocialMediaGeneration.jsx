export const generateSocialMediaPost = async (marketCopy, postType) => {
    if (!marketCopy) return [];

    var copy = {
        Copy: marketCopy,
        PostType: postType
    }

    const response = await fetch('createcopy', {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(copy)
    });
    return response.json();
};

export const getDominantColors = async (imageUri) => {
    if (!imageUri) return [];

    const response = await fetch('getcolortheme', {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ ForegroundImageUri: imageUri })
    });
    return response.json();
}

export const getBackgroundDescription = async (copy, imageUri) => {
    if (!copy || !imageUri) return [];

    const response = await fetch('getbackgrounddescription', {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            Copy: copy,
            ImageUrl: imageUri
        })
    });
    return response.json();
}

export const generateBackgrounds = async (description) => {
    if (!description) return [];

    const response = await fetch('generatebackgrounds', {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            Description: description
        })
    });
    return response.json();
}

export const removeBackgroundAndCrop = async (imageUri) => {
    if (!imageUri) return [];

    const response = await fetch('removebackgroundandcrop', {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ ForegroundImageUri: imageUri })
    });
    return response.json();
}

export const combineImages = async (backgroundImages, foregroundImage) => {
    if (!backgroundImages || !foregroundImage) return [];

    const response = await fetch('combineimages', {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ ForegroundImage: foregroundImage, BackgroundImages: backgroundImages})
    });
    return response.json();
}